using System.Net.WebSockets;
using System.Text;
using Application.Reception.Realtime;

namespace Web.Api.Realtime;

// Writes are serialized: the WebSocket protocol only allows one outstanding send per direction.
internal sealed class WebSocketRealtimeSocket : IRealtimeSocket, IAsyncDisposable
{
  private readonly WebSocket _socket;
  private readonly SemaphoreSlim _sendLock = new(1, 1);
  private int _closed;

  public WebSocketRealtimeSocket(WebSocket socket)
  {
    _socket = socket;
    SocketId = Guid.NewGuid().ToString("N");
  }

  public string SocketId { get; }

  public async Task EmitAsync(string eventName, object payload, CancellationToken cancellationToken = default)
  {
    if (Volatile.Read(ref _closed) != 0 || _socket.State != WebSocketState.Open)
    {
      return;
    }

    string frame = RealtimeEnvelope.SerializeFromObject(eventName, payload);
    byte[] bytes = Encoding.UTF8.GetBytes(frame);

    await _sendLock.WaitAsync(cancellationToken);
    try
    {
      await _socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
    }
    catch (WebSocketException)
    {
      // ignore
    }
    catch (OperationCanceledException)
    {
      // ignore
    }
    finally
    {
      _sendLock.Release();
    }
  }

  public async Task DisconnectAsync(string reason, CancellationToken cancellationToken = default)
  {
    if (Interlocked.Exchange(ref _closed, 1) != 0)
    {
      return;
    }

    if (_socket.State != WebSocketState.Open && _socket.State != WebSocketState.CloseReceived)
    {
      return;
    }

    try
    {
      await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, cancellationToken);
    }
    catch (WebSocketException)
    {
      // ignore
    }
    catch (OperationCanceledException)
    {
      // ignore
    }
  }

  public ValueTask DisposeAsync()
  {
    _sendLock.Dispose();
    return ValueTask.CompletedTask;
  }
}
