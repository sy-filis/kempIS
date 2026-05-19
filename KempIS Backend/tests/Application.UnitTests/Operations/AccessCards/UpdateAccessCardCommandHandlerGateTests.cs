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

public sealed class UpdateAccessCardCommandHandlerGateTests : HandlerTestBase
{
  private readonly IGateClient _gate = Substitute.For<IGateClient>();

  private UpdateAccessCardCommandHandler CreateSut() =>
    new(Db, _gate, NullLogger<UpdateAccessCardCommandHandler>.Instance);

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

  private async Task<AccessCard> SeedCardAsync(
    ulong uid = 300UL,
    Guid? billId = null,
    string? note = "old note",
    DateOnly? validUntil = null)
  {
    var card = new AccessCard
    {
      Id = Guid.NewGuid(),
      Uid = uid,
      BillId = billId,
      Deposit = 50m,
      ValidUntil = validUntil ?? new DateOnly(2026, 8, 10),
      IssuedAtUtc = Clock.UtcNow,
      Note = note,
    };
    Db.AccessCards.Add(card);
    await Db.SaveChangesAsync();
    Db.Entry(card).State = EntityState.Detached;
    return card;
  }

  [Fact]
  public async Task Handle_WithBill_UpdatesFieldsAndPushesPut()
  {
    Guid billId = await SeedBillAsync();
    AccessCard seeded = await SeedCardAsync(uid: 310UL, billId: billId, note: "old");

    var newValidUntil = new DateOnly(2026, 8, 20);
    var cmd = new UpdateAccessCardCommand(seeded.Id, newValidUntil, "new note");

    Result<AccessCardResponse> result = await CreateSut().Handle(cmd, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    AccessCard? persisted = await Db.AccessCards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == seeded.Id);
    persisted.ShouldNotBeNull();
    persisted.ValidUntil.ShouldBe(newValidUntil);
    persisted.Note.ShouldBe("new note");
    await _gate.Received(1).PutCardAsync(
      310UL,
      Arg.Is<GateCardPayload>(p =>
        p.RealName == "Jan Novák" &&
        p.Note == "new note" &&
        p.ValidTo == new DateTimeOffset(2026, 8, 20, 23, 59, 59, TimeSpan.FromHours(2))),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_WithoutBill_PushesPutWithEmptyRealName()
  {
    AccessCard seeded = await SeedCardAsync(uid: 320UL, billId: null, note: null);

    var cmd = new UpdateAccessCardCommand(seeded.Id, new DateOnly(2026, 9, 1), "noted");

    Result<AccessCardResponse> result = await CreateSut().Handle(cmd, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    await _gate.Received(1).PutCardAsync(
      320UL,
      Arg.Is<GateCardPayload>(p =>
        p.RealName == string.Empty &&
        p.Note == "noted"),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_GateClientThrows_StillReturnsSuccessAndPersistsChanges()
  {
    AccessCard seeded = await SeedCardAsync(uid: 330UL, billId: null, note: "orig");
    _gate.PutCardAsync(default, default!, default).ThrowsAsyncForAnyArgs(new HttpRequestException("boom"));

    var cmd = new UpdateAccessCardCommand(seeded.Id, new DateOnly(2026, 9, 5), "after-throw");

    Result<AccessCardResponse> result = await CreateSut().Handle(cmd, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    AccessCard? persisted = await Db.AccessCards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == seeded.Id);
    persisted.ShouldNotBeNull();
    persisted.ValidUntil.ShouldBe(new DateOnly(2026, 9, 5));
    persisted.Note.ShouldBe("after-throw");
  }

  [Fact]
  public async Task Handle_UnknownCard_DoesNotCallGate()
  {
    var cmd = new UpdateAccessCardCommand(Guid.NewGuid(), new DateOnly(2026, 9, 5), "anything");

    Result<AccessCardResponse> result = await CreateSut().Handle(cmd, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("AccessCards.NotFound");
    await _gate.DidNotReceiveWithAnyArgs().PutCardAsync(default, default!, default);
  }

  [Fact]
  public async Task Handle_NoteSetToNull_ClearsNote()
  {
    AccessCard seeded = await SeedCardAsync(uid: 340UL, billId: null, note: "some note");

    var cmd = new UpdateAccessCardCommand(seeded.Id, new DateOnly(2026, 9, 10), Note: null);

    Result<AccessCardResponse> result = await CreateSut().Handle(cmd, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    AccessCard? persisted = await Db.AccessCards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == seeded.Id);
    persisted.ShouldNotBeNull();
    persisted.Note.ShouldBeNull();
    await _gate.Received(1).PutCardAsync(
      340UL,
      Arg.Is<GateCardPayload>(p => p.Note == string.Empty),
      Arg.Any<CancellationToken>());
  }
}
