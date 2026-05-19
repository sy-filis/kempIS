namespace Application.Reception.Realtime;

public interface IRealtimeSocket
{
  string SocketId { get; }

  Task EmitAsync(string eventName, object payload, CancellationToken cancellationToken = default);

  Task DisconnectAsync(string reason, CancellationToken cancellationToken = default);
}
