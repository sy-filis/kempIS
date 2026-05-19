using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace TestUtilities.Realtime;

public sealed record RealtimeEcho(string Event, JsonElement Data);

public sealed class RealtimeWebSocketTestClient : IDisposable
{
  private readonly WebSocket _ws;
  private readonly CancellationTokenSource _cts = new();
  private readonly TaskCompletionSource _disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

  private readonly object _lock = new();
  private readonly Dictionary<string, Queue<TaskCompletionSource<JsonElement>>> _waiters = new();
  private readonly Dictionary<string, Queue<JsonElement>> _buffered = new();

  private WebSocketCloseStatus? _closeStatus;
  private string? _closeDescription;

  public WebSocketCloseStatus? CloseStatus => _closeStatus;

  public string? CloseDescription => _closeDescription;

  private RealtimeWebSocketTestClient(WebSocket ws)
  {
    _ws = ws;
    _ = ReceiveLoopAsync();
  }

  public static async Task<RealtimeWebSocketTestClient> ConnectAsync(
    Func<Uri, CancellationToken, Task<WebSocket>> connect,
    Uri uri,
    TimeSpan? timeout = null)
  {
    using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(10));
    WebSocket ws = await connect(uri, cts.Token);
    return new RealtimeWebSocketTestClient(ws);
  }

  public async Task EmitAsync(string eventName, object payload)
  {
    string framePayload = JsonSerializer.Serialize(payload);
    string frame = $$"""{"event":"{{eventName}}","data":{{framePayload}}}""";
    byte[] bytes = Encoding.UTF8.GetBytes(frame);
    await _ws.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, endOfMessage: true, _cts.Token);
  }

  public async Task<RealtimeEcho> WaitForAsync(string eventName, TimeSpan timeout)
  {
    TaskCompletionSource<JsonElement> tcs;
    lock (_lock)
    {
      if (_buffered.TryGetValue(eventName, out Queue<JsonElement>? buf) && buf.Count > 0)
      {
        return new RealtimeEcho(eventName, buf.Dequeue());
      }

      tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
      if (!_waiters.TryGetValue(eventName, out Queue<TaskCompletionSource<JsonElement>>? q))
      {
        q = new Queue<TaskCompletionSource<JsonElement>>();
        _waiters[eventName] = q;
      }

      q.Enqueue(tcs);
    }

    JsonElement data = await tcs.Task.WaitAsync(timeout);
    return new RealtimeEcho(eventName, data);
  }

  public Task WaitForDisconnectAsync(TimeSpan timeout) =>
    _disconnected.Task.WaitAsync(timeout);

  public void Dispose()
  {
    _cts.Cancel();
    try
    {
      if (_ws.State == WebSocketState.Open)
      {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "dispose", cts.Token).GetAwaiter().GetResult();
      }
    }
    catch { /* drain */ }

    _disconnected.TrySetResult();
    _ws.Dispose();
    _cts.Dispose();
  }

  private async Task ReceiveLoopAsync()
  {
    byte[] buffer = new byte[65536];
    try
    {
      while (_ws.State == WebSocketState.Open)
      {
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
          result = await _ws.ReceiveAsync(buffer, _cts.Token);
          if (result.MessageType == WebSocketMessageType.Close)
          {
            _closeStatus = result.CloseStatus;
            _closeDescription = result.CloseStatusDescription;
            return;
          }

          await ms.WriteAsync(buffer.AsMemory(0, result.Count), _cts.Token);
        } while (!result.EndOfMessage);

        string text = Encoding.UTF8.GetString(ms.ToArray());
        TryDispatch(text);
      }
    }
    catch (OperationCanceledException)
    {
      // drain
    }
    catch
    {
      // drain
    }
    finally
    {
      _disconnected.TrySetResult();
    }
  }

  private void TryDispatch(string json)
  {
    try
    {
      using var doc = JsonDocument.Parse(json);
      if (doc.RootElement.ValueKind != JsonValueKind.Object)
      {
        return;
      }

      if (!doc.RootElement.TryGetProperty("event", out JsonElement nameElement) ||
          nameElement.ValueKind != JsonValueKind.String)
      {
        return;
      }

      string name = nameElement.GetString()!;
      JsonElement data = doc.RootElement.TryGetProperty("data", out JsonElement dataElement)
        ? dataElement.Clone()
        : JsonDocument.Parse("{}").RootElement.Clone();

      lock (_lock)
      {
        if (_waiters.TryGetValue(name, out Queue<TaskCompletionSource<JsonElement>>? q) && q.Count > 0)
        {
          q.Dequeue().TrySetResult(data);
          return;
        }

        if (!_buffered.TryGetValue(name, out Queue<JsonElement>? buf))
        {
          buf = new Queue<JsonElement>();
          _buffered[name] = buf;
        }

        buf.Enqueue(data);
      }
    }
    catch
    {
      // drain
    }
  }
}
