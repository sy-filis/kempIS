using System.Collections.Concurrent;
using Application.Reception.Realtime;

namespace Application.UnitTests.Reception.Realtime;

internal sealed class FakeRealtimeSocket : IRealtimeSocket
{
  public string SocketId { get; } = Guid.NewGuid().ToString("N");

  public ConcurrentQueue<(string Event, object Payload)> Emitted { get; } = new();

  public List<string> Disconnects { get; } = new();

  public bool IsConnected => Disconnects.Count == 0;

  public Task EmitAsync(string eventName, object payload, CancellationToken cancellationToken = default)
  {
    Emitted.Enqueue((eventName, payload));
    return Task.CompletedTask;
  }

  public Task DisconnectAsync(string reason, CancellationToken cancellationToken = default)
  {
    Disconnects.Add(reason);
    return Task.CompletedTask;
  }
}
