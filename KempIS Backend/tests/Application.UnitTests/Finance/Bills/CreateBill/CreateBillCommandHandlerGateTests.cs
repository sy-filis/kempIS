using Application.Abstractions.Finance;
using Application.Abstractions.Gate;
using Application.Configuration;
using Application.Finance.Bills.CreateBill;
using Application.Finance.Bills.Shared;
using Domain.Common;
using Domain.Finance.Payments;
using Domain.Operations.AccessCards;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;
using SharedKernel;

namespace Application.UnitTests.Finance.Bills.CreateBill;

public sealed class CreateBillCommandHandlerGateTests : HandlerTestBase
{
  private readonly IBillNumberGenerator _numbers = Substitute.For<IBillNumberGenerator>();
  private readonly IGateClient _gate = Substitute.For<IGateClient>();

  private readonly CampSettings _campSettings = new() { CheckOutTime = new TimeOnly(11, 0) };
  private readonly RetentionSettings _retention = new()
  {
    GuestYears = 6,
    BillYears = 10,
    InvoiceYears = 10,
    RunAtLocalTime = new TimeOnly(3, 0),
  };

  private CreateBillCommandHandler CreateSut() =>
    new(Db, Clock, _numbers, Options.Create(_campSettings), Options.Create(_retention),
        _gate, NullLogger<CreateBillCommandHandler>.Instance);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private static CreateBillCommand MakeCommand(IReadOnlyList<AccessCardInput> accessCards) =>
    new(
      ReservationId: null,
      CheckInAt: new DateOnly(2026, 8, 10),
      CheckOutAt: new DateOnly(2026, 8, 15),
      Payer: new BillPayerInput("Jan", "Novák", Addr()),
      LegalEntity: null,
      PaymentType: PaymentType.Cash,
      LanguageId: Guid.NewGuid(),
      Items: [new BillItemInput(null, 1u, 100m, 21m, 1u, 1u)],
      LinkedInvoiceIds: [],
      ExistingGuests: [],
      NewGuests: [],
      ReservationSpotItemIds: [],
      AccessCards: accessCards,
      NewVehicles: [],
      ExistingVehicleIds: []);

  [Fact]
  public async Task Handle_WithNewAccessCard_PushesPutToGateWithBillPayer()
  {
    _numbers.NextAsync(Clock.UtcNow.Year, Arg.Any<CancellationToken>()).Returns("B-1");

    CreateBillCommand cmd = MakeCommand([
      new AccessCardInput(500UL, 0m, new DateOnly(2026, 8, 15), "tent key"),
    ]);

    Result<CreateBillResponse> result = await CreateSut().Handle(cmd, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    await _gate.Received(1).PutCardAsync(
      500UL,
      Arg.Is<GateCardPayload>(p =>
        p.RealName == "Jan Novák" &&
        p.Note == "tent key" &&
        p.ValidTo == new DateTimeOffset(2026, 8, 15, 23, 59, 59, TimeSpan.FromHours(2))),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_WithExistingAccessCard_PushesPutWithNewValidUntil()
  {
    Db.AccessCards.Add(new AccessCard
    {
      Id = Guid.NewGuid(),
      Uid = 600UL,
      Deposit = 5m,
      ValidUntil = new DateOnly(2025, 1, 1),
      IssuedAtUtc = Clock.UtcNow.AddDays(-30),
      Note = "old note",
    });
    await Db.SaveChangesAsync();

    _numbers.NextAsync(Clock.UtcNow.Year, Arg.Any<CancellationToken>()).Returns("B-2");

    CreateBillCommand cmd = MakeCommand([
      new AccessCardInput(600UL, 10m, new DateOnly(2026, 8, 15), "new note"),
    ]);

    Result<CreateBillResponse> result = await CreateSut().Handle(cmd, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    await _gate.Received(1).PutCardAsync(
      600UL,
      Arg.Is<GateCardPayload>(p =>
        p.RealName == "Jan Novák" &&
        p.Note == "new note" &&
        p.ValidTo == new DateTimeOffset(2026, 8, 15, 23, 59, 59, TimeSpan.FromHours(2))),
      Arg.Any<CancellationToken>());

    AccessCard updated = await Db.AccessCards.AsNoTracking().SingleAsync(c => c.Uid == 600UL);
    updated.ValidUntil.ShouldBe(new DateOnly(2026, 8, 15));
    updated.Note.ShouldBe("new note");
  }

  [Fact]
  public async Task Handle_MultipleCards_PushesOnePutPerCard()
  {
    _numbers.NextAsync(Clock.UtcNow.Year, Arg.Any<CancellationToken>()).Returns("B-3");

    CreateBillCommand cmd = MakeCommand([
      new AccessCardInput(700UL, 0m, new DateOnly(2026, 8, 15), null),
      new AccessCardInput(701UL, 0m, new DateOnly(2026, 8, 15), null),
    ]);

    Result<CreateBillResponse> result = await CreateSut().Handle(cmd, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    await _gate.Received(1).PutCardAsync(700UL, Arg.Any<GateCardPayload>(), Arg.Any<CancellationToken>());
    await _gate.Received(1).PutCardAsync(701UL, Arg.Any<GateCardPayload>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_GateClientThrows_StillReturnsCreated()
  {
    _numbers.NextAsync(Clock.UtcNow.Year, Arg.Any<CancellationToken>()).Returns("B-4");
    _gate.PutCardAsync(default, default!, default).ThrowsAsyncForAnyArgs(new HttpRequestException("boom"));

    CreateBillCommand cmd = MakeCommand([
      new AccessCardInput(800UL, 0m, new DateOnly(2026, 8, 15), null),
    ]);

    Result<CreateBillResponse> result = await CreateSut().Handle(cmd, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    (await Db.AccessCards.AsNoTracking().AnyAsync(c => c.Uid == 800UL)).ShouldBeTrue();
  }
}
