using Application.Abstractions.Gate;
using Application.Operations.AccessCards;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Operations.AccessCards;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;
using SharedKernel;

namespace Application.UnitTests.Operations.AccessCards;

public sealed class IssueAccessCardCommandHandlerGateTests : HandlerTestBase
{
  private readonly IGateClient _gate = Substitute.For<IGateClient>();

  private IssueAccessCardCommandHandler CreateSut() =>
    new(Db, Clock, _gate, NullLogger<IssueAccessCardCommandHandler>.Instance);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private async Task<Guid> SeedBillAsync(string payerName = "Jan", string payerSurname = "Novák")
  {
    var billId = Guid.NewGuid();
    Db.Bills.Add(new Bill
    {
      Id = billId,
      Number = "B-1",
      ReservationId = null,
      IssuedAtUtc = Clock.UtcNow,
      CheckInAt = new DateOnly(2026, 8, 10),
      CheckOutAt = new DateOnly(2026, 8, 15),
      LanguageIdGuid = Guid.NewGuid(),
      Payer = new Payer { Name = payerName, Surname = payerSurname, Address = Addr() },
      Payment = new Payment(PaymentType.Cash, 0m),
    });
    await Db.SaveChangesAsync();
    return billId;
  }

  [Fact]
  public async Task Handle_WithBill_CallsGateClientWithPayerName()
  {
    Guid billId = await SeedBillAsync();
    var cmd = new IssueAccessCardCommand(
      billId, 100UL, 20m, new DateOnly(2026, 8, 15), "extra key");

    Result<AccessCardResponse> result = await CreateSut().Handle(cmd, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    await _gate.Received(1).PutCardAsync(
      100UL,
      Arg.Is<GateCardPayload>(p =>
        p.RealName == "Jan Novák" &&
        p.Note == "extra key"),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_WithoutBill_CallsGateClientWithEmptyRealNameAndNoteFallback()
  {
    var cmd = new IssueAccessCardCommand(
      null, 110UL, 0m, new DateOnly(2026, 8, 15), Note: null);

    Result<AccessCardResponse> result = await CreateSut().Handle(cmd, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    await _gate.Received(1).PutCardAsync(
      110UL,
      Arg.Is<GateCardPayload>(p =>
        p.RealName == string.Empty &&
        p.Note == string.Empty),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_ValidUntil_MappedToEndOfDayEuropePragueDst()
  {
    var cmd = new IssueAccessCardCommand(
      null, 120UL, 0m, new DateOnly(2026, 8, 15), null);

    await CreateSut().Handle(cmd, CancellationToken.None);

    await _gate.Received(1).PutCardAsync(
      120UL,
      Arg.Is<GateCardPayload>(p =>
        p.ValidTo == new DateTimeOffset(2026, 8, 15, 23, 59, 59, TimeSpan.FromHours(2))),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_ValidUntil_MappedToEndOfDayEuropePragueWinter()
  {
    var cmd = new IssueAccessCardCommand(
      null, 130UL, 0m, new DateOnly(2026, 12, 15), null);

    await CreateSut().Handle(cmd, CancellationToken.None);

    await _gate.Received(1).PutCardAsync(
      130UL,
      Arg.Is<GateCardPayload>(p =>
        p.ValidTo == new DateTimeOffset(2026, 12, 15, 23, 59, 59, TimeSpan.FromHours(1))),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_GateClientThrows_StillReturnsSuccessAndPersists()
  {
    _gate.PutCardAsync(default, default!, default).ThrowsAsyncForAnyArgs(new HttpRequestException("boom"));

    var cmd = new IssueAccessCardCommand(
      null, 140UL, 0m, new DateOnly(2026, 8, 15), null);

    Result<AccessCardResponse> result = await CreateSut().Handle(cmd, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    (await Db.AccessCards.AsNoTracking().AnyAsync(c => c.Uid == 140UL)).ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_UnknownBill_DoesNotCallGate()
  {
    var cmd = new IssueAccessCardCommand(
      Guid.NewGuid(), 150UL, 0m, new DateOnly(2026, 8, 15), null);

    Result<AccessCardResponse> result = await CreateSut().Handle(cmd, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    await _gate.DidNotReceiveWithAnyArgs().PutCardAsync(default, default!, default);
  }

  [Fact]
  public async Task Handle_DuplicateUid_DoesNotCallGate()
  {
    Db.AccessCards.Add(new AccessCard
    {
      Id = Guid.NewGuid(),
      Uid = 160UL,
      Deposit = 0m,
      ValidUntil = new DateOnly(2026, 1, 1),
      IssuedAtUtc = Clock.UtcNow,
    });
    await Db.SaveChangesAsync();

    var cmd = new IssueAccessCardCommand(
      null, 160UL, 0m, new DateOnly(2026, 8, 15), null);

    Result<AccessCardResponse> result = await CreateSut().Handle(cmd, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    await _gate.DidNotReceiveWithAnyArgs().PutCardAsync(default, default!, default);
  }
}
