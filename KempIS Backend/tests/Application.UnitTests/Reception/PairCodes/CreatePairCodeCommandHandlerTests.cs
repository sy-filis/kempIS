using Application.Reception.PairCodes.Commands.CreatePairCode;
using Application.Reception.Realtime;
using SharedKernel;

namespace Application.UnitTests.Reception.PairCodes;

public sealed class CreatePairCodeCommandHandlerTests
{
  [Fact]
  public async Task Handle_DelegatesToCoordinator_ReturnsItsResponse()
  {
    IReceptionRealtimeCoordinator coordinator = Substitute.For<IReceptionRealtimeCoordinator>();
    CreatePairCodeResponse expected = new("the-code", new DateTime(2026, 5, 11, 10, 2, 0, DateTimeKind.Utc));
    coordinator.IssuePairCode().Returns(expected);

    CreatePairCodeCommandHandler handler = new(coordinator);
    Result<CreatePairCodeResponse> result = await handler.Handle(new CreatePairCodeCommand(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBe(expected);
    coordinator.Received(1).IssuePairCode();
  }
}
