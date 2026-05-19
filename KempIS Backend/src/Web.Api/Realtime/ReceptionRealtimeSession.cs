using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Application.Abstractions.Reception;
using Application.Reception.Realtime;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Web.Api.Realtime;

internal sealed partial class ReceptionRealtimeSession : IAsyncDisposable
{
  private readonly IReceptionRealtimeCoordinator _coordinator;
  private readonly ReceptionOptions _options;
  private readonly ILogger<ReceptionRealtimeSession> _logger;

  private PeerRole? _allowedRole;
  private volatile bool _hasJoinedRoom;
  private WebSocketRealtimeSocket? _adapter;
  private Timer? _graceTimer;
  private readonly Stopwatch _lifetime = Stopwatch.StartNew();

  public ReceptionRealtimeSession(
    IReceptionRealtimeCoordinator coordinator,
    IOptions<ReceptionOptions> options,
    ILogger<ReceptionRealtimeSession> logger)
  {
    _coordinator = coordinator;
    _options = options.Value;
    _logger = logger;
  }

  public ValueTask DisposeAsync() => ValueTask.CompletedTask;

  public async Task RunAsync(WebSocket socket, CancellationToken cancellationToken)
  {
    _adapter = new WebSocketRealtimeSocket(socket);

    // Force-close if pair:join does not arrive within the grace window. The timer can race
    // RunAsync's finally; DisconnectAsync is one-shot and tolerant of an already-closed socket.
    _graceTimer = new Timer(_ =>
    {
      if (!_hasJoinedRoom)
      {
        LogJoinGraceExpired(_logger, _lifetime.ElapsedMilliseconds);
        FireAndForget(_adapter.DisconnectAsync("join_timeout", CancellationToken.None));
      }
    }, state: null, TimeSpan.FromSeconds(_options.TabletJoinGraceSeconds), Timeout.InfiniteTimeSpan);

    int bufferSize = Math.Max(_options.SignaturePngMaxBytes, _options.SessionPushMaxBytes) + 4096;
    byte[] buffer = new byte[bufferSize];

    try
    {
      while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
      {
        (WebSocketReceiveResult result, byte[]? payload) = await ReceiveFullMessageAsync(socket, buffer, cancellationToken);
        if (result.MessageType == WebSocketMessageType.Close)
        {
          break;
        }

        if (result.MessageType != WebSocketMessageType.Text || payload is null)
        {
          await EmitErrorAndCloseAsync(socket, "bad_request", "only text frames are accepted", WebSocketCloseStatus.PolicyViolation, cancellationToken);
          break;
        }

        string text = Encoding.UTF8.GetString(payload);
        var envelope = RealtimeEnvelope.TryParse(text);
        if (envelope is null)
        {
          await EmitErrorAndCloseAsync(socket, "bad_request", "malformed envelope", WebSocketCloseStatus.PolicyViolation, cancellationToken);
          break;
        }

        if (envelope.Event == ReceptionEventNames.PairJoin)
        {
          if (_hasJoinedRoom)
          {
            continue;
          }

          bool keepOpen = await HandlePairJoinAsync(socket, envelope, cancellationToken);
          if (!keepOpen)
          {
            break;
          }
        }
        else
        {
          if (!await EnforceSizeLimitAsync(envelope, payload.Length, cancellationToken))
          {
            continue;
          }

          await HandleRelayAsync(envelope, cancellationToken);
        }
      }
    }
    catch (OperationCanceledException) { /* host shutdown */ }
    catch (WebSocketException ex)
    {
      LogReceiveFault(_logger, ex);
    }
    finally
    {
      if (_graceTimer is not null)
      {
        await _graceTimer.DisposeAsync();
      }

      if (_adapter is not null)
      {
        await _coordinator.RemoveSocketAsync(_adapter, CancellationToken.None);
        await _adapter.DisposeAsync();
      }

      string role = _allowedRole.HasValue ? _allowedRole.Value.ToString() : "(unassigned)";
      LogSessionEnded(_logger, role, _hasJoinedRoom, _lifetime.ElapsedMilliseconds);
    }
  }

  private static async Task<(WebSocketReceiveResult Result, byte[]? Payload)> ReceiveFullMessageAsync(
    WebSocket socket, byte[] buffer, CancellationToken ct)
  {
    using var ms = new MemoryStream();
    WebSocketReceiveResult result;
    do
    {
      result = await socket.ReceiveAsync(buffer, ct);
      if (result.MessageType == WebSocketMessageType.Close)
      {
        return (result, null);
      }

      await ms.WriteAsync(buffer.AsMemory(0, result.Count), ct);
    } while (!result.EndOfMessage);

    return (result, ms.ToArray());
  }

  private async Task<bool> HandlePairJoinAsync(WebSocket socket, RealtimeEnvelope envelope, CancellationToken ct)
  {
    string? pairCode = envelope.Data.TryGetProperty("pairCode", out JsonElement codeProp) && codeProp.ValueKind == JsonValueKind.String
      ? codeProp.GetString()
      : null;
    string? roleStr = envelope.Data.TryGetProperty("role", out JsonElement roleProp) && roleProp.ValueKind == JsonValueKind.String
      ? roleProp.GetString()
      : null;
    PeerRole? parsedRole = PeerRoleExtensions.FromWireString(roleStr);

    if (string.IsNullOrEmpty(pairCode) || parsedRole is null)
    {
      await EmitErrorAndCloseAsync(socket, "bad_request", "pair:join requires pairCode and role", WebSocketCloseStatus.PolicyViolation, ct);
      return false;
    }

    _allowedRole = parsedRole;

    Result<RoomJoinOutcome> result = await _coordinator.TryJoinRoomAsync(pairCode, parsedRole.Value, _adapter!, ct);
    if (result.IsFailure)
    {
      string code = MapErrorCode(result.Error);
      await EmitErrorAndCloseAsync(socket, code, result.Error.Description, WebSocketCloseStatus.PolicyViolation, ct);
      return false;
    }

    _hasJoinedRoom = true;
    if (_graceTimer is not null)
    {
      await _graceTimer.DisposeAsync();
      _graceTimer = null;
    }

    return true;
  }

  private async Task HandleRelayAsync(RealtimeEnvelope envelope, CancellationToken ct)
  {
    if (_adapter is null)
    {
      return;
    }

    IRealtimeSocket? peer = _coordinator.GetOtherSocket(_adapter);
    if (peer is null)
    {
      await _adapter.EmitAsync(ReceptionEventNames.Error, new { code = "not_paired", message = "this socket is not part of an active room" }, ct);
      return;
    }

    await peer.EmitAsync(envelope.Event, envelope.Data, ct);
  }

  private async Task<bool> EnforceSizeLimitAsync(RealtimeEnvelope envelope, int frameBytes, CancellationToken ct)
  {
    int limit = envelope.Event switch
    {
      ReceptionEventNames.SessionPush => _options.SessionPushMaxBytes,
      ReceptionEventNames.SignatureCaptured => _options.SignaturePngMaxBytes,
      _ => _options.DefaultEventMaxBytes,
    };

    if (frameBytes <= limit)
    {
      return true;
    }

    if (_adapter is not null)
    {
      await _adapter.EmitAsync(ReceptionEventNames.Error, new { code = "payload_too_large", message = $"max {limit} bytes for {envelope.Event}" }, ct);
    }

    return false;
  }

  private static string MapErrorCode(Error error) => error.Code switch
  {
    "Reception.Realtime.InvalidPairCode" => "invalid_pair_code",
    "Reception.Realtime.RoleTaken" => "role_taken",
    _ => "bad_request",
  };

  private async Task EmitErrorAndCloseAsync(
    WebSocket socket, string code, string message, WebSocketCloseStatus status, CancellationToken ct)
  {
    if (_adapter is not null)
    {
      try
      {
        await _adapter.EmitAsync(ReceptionEventNames.Error, new { code, message }, ct);
      }
      catch (Exception)
      {
        // ignore
      }
    }

    // Allow the error frame to reach the wire before the close handshake.
    try
    { await Task.Delay(50, ct); }
    catch (OperationCanceledException) { /* shutdown */ }

    if (socket.State == WebSocketState.Open)
    {
      try
      { await socket.CloseAsync(status, code, ct); }
      catch (Exception)
      {
        // ignore
      }
    }
  }

  private static void FireAndForget(Task task) =>
    task.ContinueWith(
      _ => { /* discard */ },
      CancellationToken.None,
      TaskContinuationOptions.OnlyOnFaulted,
      TaskScheduler.Default);

  [LoggerMessage(Level = LogLevel.Information, Message = "Reception realtime session ended. Role={Role} JoinedRoom={JoinedRoom} LifetimeMs={LifetimeMs}")]
  private static partial void LogSessionEnded(ILogger logger, string role, bool joinedRoom, long lifetimeMs);

  [LoggerMessage(Level = LogLevel.Information, Message = "Reception realtime join grace expired. LifetimeMs={LifetimeMs}")]
  private static partial void LogJoinGraceExpired(ILogger logger, long lifetimeMs);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Reception realtime receive loop ended on a WebSocket fault.")]
  private static partial void LogReceiveFault(ILogger logger, Exception ex);
}
