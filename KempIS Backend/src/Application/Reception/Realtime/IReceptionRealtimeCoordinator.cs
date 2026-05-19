using Application.Reception.PairCodes.Commands.CreatePairCode;
using SharedKernel;

namespace Application.Reception.Realtime;

public interface IReceptionRealtimeCoordinator
{
  CreatePairCodeResponse IssuePairCode();

  Task<Result<RoomJoinOutcome>> TryJoinRoomAsync(
    string pairCode,
    PeerRole role,
    IRealtimeSocket socket,
    CancellationToken cancellationToken = default);

  Task RemoveSocketAsync(IRealtimeSocket socket, CancellationToken cancellationToken = default);

  IRealtimeSocket? GetOtherSocket(IRealtimeSocket socket);
}
