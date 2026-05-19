using Application.Abstractions.Gate;
using Application.Operations.AccessCards;
using Domain.Operations.AccessCards;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;
using SharedKernel;

namespace Application.UnitTests.Operations.AccessCards;

public sealed class ReturnAccessCardCommandHandlerGateTests : HandlerTestBase
{
  private readonly IGateClient _gate = Substitute.For<IGateClient>();

  private ReturnAccessCardCommandHandler CreateSut() =>
    new(Db, _gate, NullLogger<ReturnAccessCardCommandHandler>.Instance);

  private async Task<AccessCard> SeedCardAsync(ulong uid = 200UL)
  {
    var card = new AccessCard
    {
      Id = Guid.NewGuid(),
      Uid = uid,
      Deposit = 0m,
      ValidUntil = new DateOnly(2026, 8, 15),
      IssuedAtUtc = Clock.UtcNow,
    };
    Db.AccessCards.Add(card);
    await Db.SaveChangesAsync();
    return card;
  }

  [Fact]
  public async Task Handle_Success_CallsGateClientWithUid_AndDeletesRow()
  {
    AccessCard card = await SeedCardAsync(uid: 210UL);

    Result result = await CreateSut().Handle(new ReturnAccessCardCommand(card.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    await _gate.Received(1).DeleteCardAsync(210UL, Arg.Any<CancellationToken>());
    (await Db.AccessCards.AsNoTracking().AnyAsync(c => c.Id == card.Id)).ShouldBeFalse();
  }

  [Fact]
  public async Task Handle_GateClientThrows_StillReturnsSuccessAndDeletes()
  {
    AccessCard card = await SeedCardAsync(uid: 220UL);
    _gate.DeleteCardAsync(default, default).ThrowsAsyncForAnyArgs(new HttpRequestException("boom"));

    Result result = await CreateSut().Handle(new ReturnAccessCardCommand(card.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    (await Db.AccessCards.AsNoTracking().AnyAsync(c => c.Id == card.Id)).ShouldBeFalse();
  }

  [Fact]
  public async Task Handle_UnknownCard_DoesNotCallGate()
  {
    Result result = await CreateSut().Handle(new ReturnAccessCardCommand(Guid.NewGuid()), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    await _gate.DidNotReceiveWithAnyArgs().DeleteCardAsync(default, default);
  }
}
