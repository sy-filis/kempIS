using Application.Abstractions.Messaging;
using Application.Reception.Realtime;
using SharedKernel;

namespace Application.Reception.PairCodes.Commands.CreatePairCode;

internal sealed class CreatePairCodeCommandHandler(IReceptionRealtimeCoordinator coordinator)
  : ICommandHandler<CreatePairCodeCommand, CreatePairCodeResponse>
{
  public Task<Result<CreatePairCodeResponse>> Handle(
    CreatePairCodeCommand command,
    CancellationToken cancellationToken)
  {
    CreatePairCodeResponse response = coordinator.IssuePairCode();
    return Task.FromResult(Result.Success(response));
  }
}
