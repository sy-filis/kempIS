using System.Collections.Concurrent;
using System.Security.Cryptography;
using Application.Abstractions.Reception;
using Application.Reception.PairCodes.Commands.CreatePairCode;
using Application.Reception.Realtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.Reception;

internal sealed partial class InMemoryReceptionRealtimeCoordinator : IReceptionRealtimeCoordinator, IDisposable
{
  private readonly IDateTimeProvider _clock;
  private readonly ReceptionOptions _options;
  private readonly ILogger<InMemoryReceptionRealtimeCoordinator> _logger;
  private readonly ConcurrentDictionary<string, PairCodeState> _allowlist = new();
  private readonly SemaphoreSlim _gate = new(1, 1);
  private readonly Timer _sweepTimer;
  private RoomState? _pendingRoom;
  private RoomState? _room;

  public InMemoryReceptionRealtimeCoordinator(
    IDateTimeProvider clock,
    IOptions<ReceptionOptions> options,
    ILogger<InMemoryReceptionRealtimeCoordinator> logger)
  {
    _clock = clock;
    _options = options.Value;
    _logger = logger;
    var interval = TimeSpan.FromSeconds(_options.AllowlistSweepIntervalSeconds);
    _sweepTimer = new Timer(_ => SweepExpiredAllowlistEntries(), state: null, interval, interval);
  }

  public CreatePairCodeResponse IssuePairCode()
  {
    byte[] bytes = RandomNumberGenerator.GetBytes(32);
    string pairCode = Base64UrlEncode(bytes);
    DateTime expiresAtUtc = _clock.UtcNow.AddSeconds(_options.PairCodeTtlSeconds);
    _allowlist[pairCode] = new PairCodeState(expiresAtUtc);
    LogPairCodeIssued(_logger, expiresAtUtc);
    return new CreatePairCodeResponse(pairCode, expiresAtUtc);
  }

  public async Task<Result<RoomJoinOutcome>> TryJoinRoomAsync(
    string pairCode, PeerRole role, IRealtimeSocket socket, CancellationToken cancellationToken = default)
  {
    await _gate.WaitAsync(cancellationToken);
    try
    {
      if (!_allowlist.TryGetValue(pairCode, out PairCodeState? state) ||
          state.ExpiresAtUtc <= _clock.UtcNow)
      {
        _allowlist.TryRemove(pairCode, out _);
        return Result.Failure<RoomJoinOutcome>(RealtimeErrors.InvalidPairCode);
      }

      // Second peer arrives at an existing half-joined room.
      if (_pendingRoom is not null && _pendingRoom.PairCode == pairCode)
      {
        if (_pendingRoom.GetSocket(role) is not null)
        {
          return Result.Failure<RoomJoinOutcome>(RealtimeErrors.RoleTaken);
        }

        _pendingRoom.SetSocket(role, socket);
        _allowlist.TryRemove(pairCode, out _);

        if (_room is not null)
        {
          await DisplaceCurrentRoomAsync(cancellationToken);
        }

        _room = _pendingRoom;
        _pendingRoom = null;
        _room.IsReady = true;

        await _room.Desktop!.EmitAsync(
          ReceptionEventNames.PairReady,
          new { peerRole = PeerRole.Tablet.ToWireString() },
          cancellationToken);
        await _room.Tablet!.EmitAsync(
          ReceptionEventNames.PairReady,
          new { peerRole = PeerRole.Desktop.ToWireString() },
          cancellationToken);

        PeerRole otherPeer = role == PeerRole.Desktop ? PeerRole.Tablet : PeerRole.Desktop;
        return Result.Success(new RoomJoinOutcome(pairCode, RoomReady: true, OtherPeer: otherPeer));
      }

      // Single-use: reject if this code already maps to a ready room.
      if (_room is not null && _room.PairCode == pairCode)
      {
        return Result.Failure<RoomJoinOutcome>(RealtimeErrors.InvalidPairCode);
      }

      // First-wins: drop any stale half-joined room with a different code.
      if (_pendingRoom is not null && _pendingRoom.PairCode != pairCode)
      {
        _pendingRoom = null;
      }

      _pendingRoom ??= new RoomState(pairCode);
      if (_pendingRoom.GetSocket(role) is not null)
      {
        return Result.Failure<RoomJoinOutcome>(RealtimeErrors.RoleTaken);
      }

      _pendingRoom.SetSocket(role, socket);
      return Result.Success(new RoomJoinOutcome(pairCode, RoomReady: false, OtherPeer: null));
    }
    finally
    {
      _gate.Release();
    }
  }

  public IRealtimeSocket? GetOtherSocket(IRealtimeSocket socket)
  {
    // Unlocked: a racing disconnect may drop one in-flight event during teardown.
    RoomState? room = _room;
    return room?.OtherSocket(socket);
  }

  public async Task RemoveSocketAsync(IRealtimeSocket socket, CancellationToken cancellationToken = default)
  {
    await _gate.WaitAsync(cancellationToken);
    try
    {
      if (_room is not null && _room.Contains(socket))
      {
        PeerRole leavingRole = _room.RoleOf(socket)!.Value;
        IRealtimeSocket? other = _room.OtherSocket(socket);
        _room = null;

        if (other is not null)
        {
          await other.EmitAsync(
            ReceptionEventNames.PairPeerLeft,
            new { peerRole = leavingRole.ToWireString() },
            cancellationToken);
        }

        return;
      }

      if (_pendingRoom is not null && _pendingRoom.Contains(socket))
      {
        _pendingRoom = null;
      }
    }
    finally
    {
      _gate.Release();
    }
  }

  private async Task DisplaceCurrentRoomAsync(CancellationToken cancellationToken)
  {
    RoomState? old = _room;
    _room = null;
    if (old is null)
    {
      return;
    }

    object payload = new { reason = "new_pair" };
    if (old.Desktop is not null)
    {
      await old.Desktop.EmitAsync(ReceptionEventNames.PairDisplaced, payload, cancellationToken);
      await old.Desktop.DisconnectAsync("displaced", cancellationToken);
    }

    if (old.Tablet is not null)
    {
      await old.Tablet.EmitAsync(ReceptionEventNames.PairDisplaced, payload, cancellationToken);
      await old.Tablet.DisconnectAsync("displaced", cancellationToken);
    }
  }

  internal void SweepExpiredAllowlistEntries()
  {
    DateTime now = _clock.UtcNow;
    foreach (KeyValuePair<string, PairCodeState> entry in _allowlist)
    {
      if (entry.Value.ExpiresAtUtc <= now)
      {
        _allowlist.TryRemove(entry.Key, out _);
      }
    }
  }

  public void Dispose()
  {
    _sweepTimer.Dispose();
    _gate.Dispose();
  }

  [LoggerMessage(Level = LogLevel.Debug, Message = "Pair code issued, expires at {ExpiresAtUtc}")]
  private static partial void LogPairCodeIssued(ILogger logger, DateTime expiresAtUtc);

  private static string Base64UrlEncode(byte[] bytes) =>
    Convert.ToBase64String(bytes)
      .Replace('+', '-')
      .Replace('/', '_')
      .TrimEnd('=');

  private sealed record PairCodeState(DateTime ExpiresAtUtc);

  private sealed class RoomState
  {
    public RoomState(string pairCode) => PairCode = pairCode;

    public string PairCode { get; }

    public IRealtimeSocket? Desktop { get; private set; }

    public IRealtimeSocket? Tablet { get; private set; }

    public bool IsReady { get; set; }

    public IRealtimeSocket? GetSocket(PeerRole role) =>
      role == PeerRole.Desktop ? Desktop : Tablet;

    public void SetSocket(PeerRole role, IRealtimeSocket socket)
    {
      if (role == PeerRole.Desktop)
      {
        Desktop = socket;
      }
      else
      {
        Tablet = socket;
      }
    }

    public bool Contains(IRealtimeSocket socket) =>
      ReferenceEquals(Desktop, socket) || ReferenceEquals(Tablet, socket);

    public PeerRole? RoleOf(IRealtimeSocket socket)
    {
      if (ReferenceEquals(Desktop, socket))
      {
        return PeerRole.Desktop;
      }

      if (ReferenceEquals(Tablet, socket))
      {
        return PeerRole.Tablet;
      }

      return null;
    }

    public IRealtimeSocket? OtherSocket(IRealtimeSocket socket)
    {
      if (ReferenceEquals(Desktop, socket))
      {
        return Tablet;
      }

      if (ReferenceEquals(Tablet, socket))
      {
        return Desktop;
      }

      return null;
    }
  }
}
