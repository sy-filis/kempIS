using Application.Abstractions.Finance;
using Application.Abstractions.Gate;
using Application.Configuration;
using Application.Finance.Bills.CreateBill;
using Application.Finance.Bills.Shared;
using Domain.Common;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.InvoiceItems;
using Domain.Finance.Invoices;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Operations.AccessCards;
using Domain.Reservations.Guests;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.Vehicles;
using Domain.Services.Services;
using Domain.Services.VatRates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.UnitTests.Finance.Bills;

public sealed class CreateBillCommandHandlerTests : HandlerTestBase
{
  private readonly IBillNumberGenerator _numbers = Substitute.For<IBillNumberGenerator>();
  private readonly IGateClient _gate = Substitute.For<IGateClient>();

  private readonly CampSettings _campSettings = new() { CheckOutTime = new TimeOnly(11, 0) };

  private readonly RetentionSettings _retentionSettings = new()
  {
    GuestYears = 6,
    BillYears = 10,
    InvoiceYears = 10,
    RunAtLocalTime = new TimeOnly(3, 0),
  };

  private CreateBillCommandHandler CreateSut() =>
    new(Db, Clock, _numbers, Options.Create(_campSettings), Options.Create(_retentionSettings),
        _gate, NullLogger<CreateBillCommandHandler>.Instance);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private static Guest NewGuest(Guid id, Guid? reservationId, Guid? billId = null) => new()
  {
    Id = id,
    ReservationId = reservationId,
    BillId = billId,
    FirstName = "G",
    LastName = "G",
    NationalityId = Guid.NewGuid(),
    DateOfBirth = new DateOnly(1990, 1, 1),
    DocumentType = DocumentType.IdCard,
    DocumentNumber = "D1",
    Address = Addr(),
    ReasonOfStay = "Holiday",
    StayDateRange = new DateRange(new DateOnly(2026, 4, 20), new DateOnly(2026, 4, 22)),
  };

  private CreateBillCommand MakeReservationCommand(
    Guid reservationId,
    IReadOnlyList<Guid> existingGuestIds,
    IReadOnlyList<Guid> linkedInvoiceIds,
    IReadOnlyList<Guid>? reservationSpotItemIds = null) =>
    new(
      reservationId,
      new DateOnly(2026, 4, 20),
      new DateOnly(2026, 4, 22),
      new BillPayerInput("John", "Doe", Addr()),
      new BillLegalEntityInput("Acme", "123", "CZ123", Addr()),
      PaymentType.Card,
      Guid.NewGuid(),
      [new BillItemInput(null, 2u, 500m, 21m, 1u, 2u)],
      linkedInvoiceIds,
      [.. existingGuestIds.Select(id => new ExistingGuestOnBillInput(id, PaysRecreationFee: true))],
      [],
      reservationSpotItemIds ?? [],
      [],
      [],
      []);

  private async Task<Guid> SeedPaidInvoice(Guid reservationId, decimal itemAmount, bool alreadyLinked = false)
  {
    var id = Guid.NewGuid();
    Db.Invoices.Add(new Invoice
    {
      Id = id,
      ReservationId = reservationId,
      Status = InvoiceStatus.Paid,
      Number = $"EXT-{Guid.NewGuid():N}"[..8],
      IssuedAt = DateOnly.FromDateTime(DateTime.UtcNow),
      PaidAt = DateOnly.FromDateTime(DateTime.UtcNow),
      LinkedBillId = alreadyLinked ? Guid.NewGuid() : null,
      Email = "seed@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    });
    Db.InvoiceItems.Add(new InvoiceItem
    {
      Id = Guid.NewGuid(),
      InvoiceId = id,
      ServiceGuid = Guid.NewGuid(),
      Quantity = 1m,
      UnitPrice = itemAmount,
      VatRatePercentage = 0m,
    });
    await Db.SaveChangesAsync();
    return id;
  }

  [Fact]
  public async Task Handle_CreatesBill_WithReservation_AndLinksExistingGuest_AndDeductsPaidInvoice()
  {
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));
    Guid invoiceId = await SeedPaidInvoice(reservationId, 1000m);

    _numbers.NextAsync(Clock.UtcNow.Year, Arg.Any<CancellationToken>()).Returns("2026/0001");

    Result<CreateBillResponse> result = await CreateSut()
      .Handle(MakeReservationCommand(reservationId, [guestId], [invoiceId]), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Bill bill = (await Db.Bills.FindAsync(result.Value.BillId))!;
    bill.Number.ShouldBe("2026/0001");
    bill.Kind.ShouldBe(BillKind.Regular);
    bill.ReservationId.ShouldBe(reservationId);

    (await Db.BillItems.CountAsync(i => i.BillId == bill.Id)).ShouldBe(1);

    Guest reloaded = (await Db.Guests.FindAsync(guestId))!;
    reloaded.BillId.ShouldBe(bill.Id);

    Invoice linkedInvoice = (await Db.Invoices.FindAsync(invoiceId))!;
    linkedInvoice.LinkedBillId.ShouldBe(bill.Id);

    var expectedCheckOut = new DateOnly(2026, 4, 22).ToDateTime(new TimeOnly(11, 0), DateTimeKind.Utc);
    reloaded.CheckOutAt.ShouldBe(expectedCheckOut);
  }

  [Fact]
  public async Task Handle_CreatesWalkInBill_WithNewGuest_NoReservation_NoInvoices()
  {
    _numbers.NextAsync(Clock.UtcNow.Year, Arg.Any<CancellationToken>()).Returns("2026/0002");

    var command = new CreateBillCommand(
      null,
      new DateOnly(2026, 4, 22),
      new DateOnly(2026, 4, 23),
      new BillPayerInput("T", "Tent", Addr()),
      new BillLegalEntityInput("Acme", "123", "CZ123", Addr()),
      PaymentType.Cash,
      Guid.NewGuid(),
      [new BillItemInput(null, 1u, 300m, 21m, 0u, 0u)],
      [],
      [],
      [new NewGuestInput(
        "Walk", "In", Guid.NewGuid(), new DateOnly(1990, 1, 1),
        DocumentType.IdCard, "D1", Addr(), "Holiday",
        new DateOnly(2026, 4, 22), new DateOnly(2026, 4, 23),
        null, null, PaysRecreationFee: true)],
      [],
      [],
      [],
      []);

    Result<CreateBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Bill bill = (await Db.Bills.FindAsync(result.Value.BillId))!;
    bill.ReservationId.ShouldBeNull();

    List<Guest> guests = [.. Db.Guests.Where(g => g.BillId == bill.Id)];
    guests.Count.ShouldBe(1);
    guests[0].ReservationId.ShouldBeNull();
    guests[0].FirstName.ShouldBe("Walk");
  }

  [Fact]
  public async Task Handle_Fails_WhenLinkingUnpaidInvoice()
  {
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));

    var invoiceId = Guid.NewGuid();
    Db.Invoices.Add(new Invoice
    {
      Id = invoiceId,
      ReservationId = reservationId,
      Status = InvoiceStatus.Created,
      Number = "EXT",
      IssuedAt = DateOnly.FromDateTime(DateTime.UtcNow),
      Email = "seed@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    });
    await Db.SaveChangesAsync();
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0001");

    Result<CreateBillResponse> result = await CreateSut()
      .Handle(MakeReservationCommand(reservationId, [guestId], [invoiceId]), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Invoice.NotPaid");
  }

  [Fact]
  public async Task Handle_Fails_WhenLinkingAlreadyLinkedInvoice()
  {
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));
    Guid invoiceId = await SeedPaidInvoice(reservationId, 100m, alreadyLinked: true);
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0001");

    Result<CreateBillResponse> result = await CreateSut()
      .Handle(MakeReservationCommand(reservationId, [guestId], [invoiceId]), CancellationToken.None);

    result.Error.Code.ShouldBe("Invoice.AlreadyLinkedToBill");
  }

  [Fact]
  public async Task Handle_Fails_WhenInvoiceFromDifferentReservation()
  {
    var reservationId = Guid.NewGuid();
    var otherReservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));
    Guid invoiceId = await SeedPaidInvoice(otherReservationId, 100m);
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0001");

    Result<CreateBillResponse> result = await CreateSut()
      .Handle(MakeReservationCommand(reservationId, [guestId], [invoiceId]), CancellationToken.None);

    result.Error.Code.ShouldBe("Invoice.ReservationMismatch");
  }

  [Fact]
  public async Task Handle_Fails_WhenExistingGuestAlreadyLinkedToAnotherBill()
  {
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId, billId: Guid.NewGuid()));
    await Db.SaveChangesAsync();
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0001");

    Result<CreateBillResponse> result = await CreateSut()
      .Handle(MakeReservationCommand(reservationId, [guestId], []), CancellationToken.None);

    result.Error.Code.ShouldBe("Bill.GuestAlreadyLinkedToAnotherBill");
  }

  [Fact]
  public async Task Handle_ComputesPaymentAmount_AsGrossMinusDeductions()
  {
    // UnitPrice is gross (VAT-inclusive). command gross = 1×2×500 = 1000;
    // invoice gross = 1×1000 = 1000; payment = 1000 − 1000 = 0.
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));

    var invoiceId = Guid.NewGuid();
    Db.Invoices.Add(new Invoice
    {
      Id = invoiceId,
      ReservationId = reservationId,
      Status = InvoiceStatus.Paid,
      Number = "INV-001",
      IssuedAt = DateOnly.FromDateTime(DateTime.UtcNow),
      PaidAt = DateOnly.FromDateTime(DateTime.UtcNow),
      Email = "seed@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    });
    Db.InvoiceItems.Add(new InvoiceItem
    {
      Id = Guid.NewGuid(),
      InvoiceId = invoiceId,
      ServiceGuid = Guid.NewGuid(),
      Quantity = 1m,
      UnitPrice = 1000m,
      VatRatePercentage = 0m,
    });
    await Db.SaveChangesAsync();

    _numbers.NextAsync(Clock.UtcNow.Year, Arg.Any<CancellationToken>()).Returns("2026/0010");

    var command = new CreateBillCommand(
      reservationId,
      new DateOnly(2026, 4, 20),
      new DateOnly(2026, 4, 22),
      new BillPayerInput("John", "Doe", Addr()),
      new BillLegalEntityInput("Acme", "123", "CZ123", Addr()),
      PaymentType.Card,
      Guid.NewGuid(),
      [new BillItemInput(null, 2u, 500m, 21m, 1u, 2u)],
      [invoiceId],
      [new ExistingGuestOnBillInput(guestId, PaysRecreationFee: true)],
      [],
      [],
      [],
      [],
      []);

    Result<CreateBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Bill bill = (await Db.Bills.FindAsync(result.Value.BillId))!;
    bill.Payment.Amount.ShouldBe(0m);
  }

  [Fact]
  public async Task Handle_Fails_WhenDeductionsExceedItemsTotal()
  {
    // command gross = 1×100×1.00 = 100; invoice gross = 1×500×1.00 = 500.
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));

    var invoiceId = Guid.NewGuid();
    Db.Invoices.Add(new Invoice
    {
      Id = invoiceId,
      ReservationId = reservationId,
      Status = InvoiceStatus.Paid,
      Number = "INV-002",
      IssuedAt = DateOnly.FromDateTime(DateTime.UtcNow),
      PaidAt = DateOnly.FromDateTime(DateTime.UtcNow),
      Email = "seed@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    });
    Db.InvoiceItems.Add(new InvoiceItem
    {
      Id = Guid.NewGuid(),
      InvoiceId = invoiceId,
      ServiceGuid = Guid.NewGuid(),
      Quantity = 1m,
      UnitPrice = 500m,
      VatRatePercentage = 0m,
    });
    await Db.SaveChangesAsync();

    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0011");

    var command = new CreateBillCommand(
      reservationId,
      new DateOnly(2026, 4, 20),
      new DateOnly(2026, 4, 22),
      new BillPayerInput("John", "Doe", Addr()),
      new BillLegalEntityInput("Acme", "123", "CZ123", Addr()),
      PaymentType.Card,
      Guid.NewGuid(),
      [new BillItemInput(null, 1u, 100m, 0m, 1u, 1u)],
      [invoiceId],
      [new ExistingGuestOnBillInput(guestId, PaysRecreationFee: true)],
      [],
      [],
      [],
      [],
      []);

    Result<CreateBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Bill.DeductionsExceedItemsTotal");
  }

  [Fact]
  public async Task Handle_Fails_WhenDuplicateLinkedInvoiceIds()
  {
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));
    await Db.SaveChangesAsync();

    var invoiceId = Guid.NewGuid();
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0012");

    var command = new CreateBillCommand(
      reservationId,
      new DateOnly(2026, 4, 20),
      new DateOnly(2026, 4, 22),
      new BillPayerInput("John", "Doe", Addr()),
      new BillLegalEntityInput("Acme", "123", "CZ123", Addr()),
      PaymentType.Card,
      Guid.NewGuid(),
      [new BillItemInput(null, 1u, 100m, 0m, 1u, 1u)],
      [invoiceId, invoiceId],
      [new ExistingGuestOnBillInput(guestId, PaysRecreationFee: true)],
      [],
      [],
      [],
      [],
      []);

    Result<CreateBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Bill.DuplicateInvoiceIds");
  }

  [Fact]
  public async Task Handle_StampsCheckOutAt_OnNewGuests_FromCampSettings()
  {
    var reservationId = Guid.NewGuid();
    _numbers.NextAsync(Clock.UtcNow.Year, Arg.Any<CancellationToken>()).Returns("2026/0010");

    CreateBillCommand command = MakeReservationCommand(reservationId, [], []) with
    {
      NewGuests =
      [
        new NewGuestInput(
          FirstName: "New",
          LastName: "Guest",
          NationalityId: Guid.NewGuid(),
          DateOfBirth: new DateOnly(1990, 1, 1),
          DocumentType: DocumentType.IdCard,
          DocumentNumber: "X1",
          Address: Addr(),
          ReasonOfStay: "Holiday",
          StayFrom: new DateOnly(2026, 4, 20),
          StayTo: new DateOnly(2026, 4, 22),
          VisaNumber: null,
          Note: null,
          PaysRecreationFee: true),
      ],
    };

    Result<CreateBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest created = await Db.Guests.FirstAsync(g => g.BillId == result.Value.BillId);
    var expectedCheckOut = new DateOnly(2026, 4, 22).ToDateTime(new TimeOnly(11, 0), DateTimeKind.Utc);
    created.CheckOutAt.ShouldBe(expectedCheckOut);
  }

  [Fact]
  public async Task Handle_StampsCheckOutAt_OnExistingGuest_OverwritingPreviousValue()
  {
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    Guest preexisting = NewGuest(guestId, reservationId);
    preexisting.CheckOutAt = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    Db.Guests.Add(preexisting);
    await Db.SaveChangesAsync();

    _numbers.NextAsync(Clock.UtcNow.Year, Arg.Any<CancellationToken>()).Returns("2026/0011");

    Result<CreateBillResponse> result = await CreateSut()
      .Handle(MakeReservationCommand(reservationId, [guestId], []), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest reloaded = (await Db.Guests.FindAsync(guestId))!;
    var expectedCheckOut = new DateOnly(2026, 4, 22).ToDateTime(new TimeOnly(11, 0), DateTimeKind.Utc);
    reloaded.CheckOutAt.ShouldBe(expectedCheckOut);
  }

  [Fact]
  public async Task Handle_LinksSpotItemsToBill_WhenSupplied()
  {
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));

    var spotA = Guid.NewGuid();
    var spotB = Guid.NewGuid();
    Db.ReservationSpotItems.Add(new ReservationSpotItem
    {
      Id = spotA,
      ReservationId = reservationId,
      SpotGroupId = Guid.NewGuid(),
      SpotId = Guid.NewGuid(),
    });
    Db.ReservationSpotItems.Add(new ReservationSpotItem
    {
      Id = spotB,
      ReservationId = reservationId,
      SpotGroupId = Guid.NewGuid(),
      SpotId = Guid.NewGuid(),
    });
    await Db.SaveChangesAsync();

    _numbers.NextAsync(Clock.UtcNow.Year, Arg.Any<CancellationToken>()).Returns("2026/0020");

    Result<CreateBillResponse> result = await CreateSut()
      .Handle(MakeReservationCommand(reservationId, [guestId], [], [spotA, spotB]), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    List<ReservationSpotItem> reloaded = [.. Db.ReservationSpotItems
      .AsNoTracking()
      .Where(s => s.ReservationId == reservationId)];
    reloaded.Count.ShouldBe(2);
    reloaded.ShouldAllBe(s => s.BillId == result.Value.BillId);
  }

  [Fact]
  public async Task Handle_Fails_WhenSpotItemAlreadyLinkedToAnotherBill()
  {
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));

    var spotId = Guid.NewGuid();
    Db.ReservationSpotItems.Add(new ReservationSpotItem
    {
      Id = spotId,
      ReservationId = reservationId,
      SpotGroupId = Guid.NewGuid(),
      SpotId = Guid.NewGuid(),
      BillId = Guid.NewGuid(),
    });
    await Db.SaveChangesAsync();
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0021");

    Result<CreateBillResponse> result = await CreateSut()
      .Handle(MakeReservationCommand(reservationId, [guestId], [], [spotId]), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Bill.SpotItemAlreadyLinkedToAnotherBill");
  }

  [Fact]
  public async Task Handle_Fails_WhenSpotItemFromAnotherReservation()
  {
    var reservationId = Guid.NewGuid();
    var otherReservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));

    var spotId = Guid.NewGuid();
    Db.ReservationSpotItems.Add(new ReservationSpotItem
    {
      Id = spotId,
      ReservationId = otherReservationId,
      SpotGroupId = Guid.NewGuid(),
      SpotId = Guid.NewGuid(),
    });
    await Db.SaveChangesAsync();
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0022");

    Result<CreateBillResponse> result = await CreateSut()
      .Handle(MakeReservationCommand(reservationId, [guestId], [], [spotId]), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Bill.SpotItemNotInReservation");
  }

  [Fact]
  public async Task Handle_Fails_WhenSpotItemDoesNotExist()
  {
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));
    await Db.SaveChangesAsync();
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0023");

    Result<CreateBillResponse> result = await CreateSut()
      .Handle(MakeReservationCommand(reservationId, [guestId], [], [Guid.NewGuid()]), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("ReservationSpotItem.NotFound");
  }

  [Fact]
  public async Task Handle_SetsHasGivenKeyTrue_OnEachLinkedSpotItem()
  {
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));

    var spotA = Guid.NewGuid();
    var spotB = Guid.NewGuid();
    var spotC = Guid.NewGuid();
    Db.ReservationSpotItems.Add(new ReservationSpotItem
    {
      Id = spotA,
      ReservationId = reservationId,
      SpotGroupId = Guid.NewGuid(),
      SpotId = Guid.NewGuid(),
      HasGivenKey = false,
    });
    Db.ReservationSpotItems.Add(new ReservationSpotItem
    {
      Id = spotB,
      ReservationId = reservationId,
      SpotGroupId = Guid.NewGuid(),
      SpotId = Guid.NewGuid(),
      HasGivenKey = false,
    });
    Db.ReservationSpotItems.Add(new ReservationSpotItem
    {
      Id = spotC,
      ReservationId = reservationId,
      SpotGroupId = Guid.NewGuid(),
      SpotId = Guid.NewGuid(),
      HasGivenKey = false,
    });
    await Db.SaveChangesAsync();

    _numbers.NextAsync(Clock.UtcNow.Year, Arg.Any<CancellationToken>()).Returns("2026/0030");

    // Link only spotA and spotB; leave spotC unlinked to verify it stays HasGivenKey = false.
    Result<CreateBillResponse> result = await CreateSut()
      .Handle(MakeReservationCommand(reservationId, [guestId], [], [spotA, spotB]), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    ReservationSpotItem reloadedA = (await Db.ReservationSpotItems.AsNoTracking().FirstAsync(s => s.Id == spotA))!;
    ReservationSpotItem reloadedB = (await Db.ReservationSpotItems.AsNoTracking().FirstAsync(s => s.Id == spotB))!;
    ReservationSpotItem reloadedC = (await Db.ReservationSpotItems.AsNoTracking().FirstAsync(s => s.Id == spotC))!;

    reloadedA.HasGivenKey.ShouldBeTrue();
    reloadedB.HasGivenKey.ShouldBeTrue();
    reloadedC.HasGivenKey.ShouldBeFalse();
  }

  [Fact]
  public async Task Handle_SetsScartationFromConfig()
  {
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));
    await Db.SaveChangesAsync();
    _numbers.NextAsync(Clock.UtcNow.Year, Arg.Any<CancellationToken>()).Returns("2026/0099");

    Result<CreateBillResponse> result = await CreateSut()
      .Handle(MakeReservationCommand(reservationId, [guestId], []), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Bill bill = (await Db.Bills.FindAsync(result.Value.BillId))!;
    bill.Scartation.ShouldBe(DateOnly.FromDateTime(bill.IssuedAtUtc).AddYears(_retentionSettings.BillYears));
  }

  [Fact]
  public async Task Handle_SetsScartationOnInlineNewGuests_FromConfig()
  {
    var reservationId = Guid.NewGuid();
    _numbers.NextAsync(Clock.UtcNow.Year, Arg.Any<CancellationToken>()).Returns("2026/0100");

    var stayTo = new DateOnly(2026, 4, 22);
    CreateBillCommand command = MakeReservationCommand(reservationId, [], []) with
    {
      NewGuests =
      [
        new NewGuestInput(
          FirstName: "New",
          LastName: "Guest",
          NationalityId: Guid.NewGuid(),
          DateOfBirth: new DateOnly(1990, 1, 1),
          DocumentType: DocumentType.IdCard,
          DocumentNumber: "X1",
          Address: Addr(),
          ReasonOfStay: "Holiday",
          StayFrom: new DateOnly(2026, 4, 20),
          StayTo: stayTo,
          VisaNumber: null,
          Note: null,
          PaysRecreationFee: true),
      ],
    };

    Result<CreateBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest created = await Db.Guests.FirstAsync(g => g.BillId == result.Value.BillId);
    created.Scartation.ShouldBe(stayTo.AddYears(_retentionSettings.GuestYears));
  }

  [Fact]
  public async Task Handle_CreatesAccessCards_LinkedToBill()
  {
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));
    await Db.SaveChangesAsync();
    _numbers.NextAsync(Clock.UtcNow.Year, Arg.Any<CancellationToken>()).Returns("2026/0200");

    CreateBillCommand command = MakeReservationCommand(reservationId, [guestId], []) with
    {
      AccessCards =
      [
        new AccessCardInput(Uid: 5001UL, Deposit: 50m, ValidUntil: new DateOnly(2026, 8, 15), Note: "x"),
        new AccessCardInput(Uid: 5002UL, Deposit: 0m, ValidUntil: new DateOnly(2026, 8, 15), Note: null),
      ],
    };

    Result<CreateBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    List<AccessCard> cards =
      [.. Db.AccessCards.Where(c => c.BillId == result.Value.BillId).AsEnumerable().OrderBy(c => c.Uid)];
    cards.Count.ShouldBe(2);
    cards[0].Uid.ShouldBe(5001UL);
    cards[0].Deposit.ShouldBe(50m);
    cards[0].ValidUntil.ShouldBe(new DateOnly(2026, 8, 15));
    cards[0].Note.ShouldBe("x");
    cards[0].IssuedAtUtc.ShouldBe(Clock.UtcNow);
    cards[1].Uid.ShouldBe(5002UL);
    cards[1].Deposit.ShouldBe(0m);
    cards[1].ValidUntil.ShouldBe(new DateOnly(2026, 8, 15));
    cards[1].Note.ShouldBeNull();
    cards[1].IssuedAtUtc.ShouldBe(Clock.UtcNow);
  }

  [Fact]
  public async Task Handle_OverwritesExistingAccessCard_WhenUidAlreadyKnown()
  {
    // Access cards are physically reusable: an existing UID is transferred to the new
    // bill in place rather than duplicated.
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    var previousBillId = Guid.NewGuid();
    var existingCardId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));
    Db.AccessCards.Add(new AccessCard
    {
      Id = existingCardId,
      Uid = 99UL,
      BillId = previousBillId,
      Deposit = 50m,
      IssuedAtUtc = Clock.UtcNow.AddDays(-30),
      Note = "old note",
    });
    await Db.SaveChangesAsync();
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0201");

    CreateBillCommand command = MakeReservationCommand(reservationId, [guestId], []) with
    {
      AccessCards = [new AccessCardInput(Uid: 99UL, Deposit: 75m, ValidUntil: new DateOnly(2026, 8, 15), Note: "new note")],
    };

    Result<CreateBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    (await Db.AccessCards.CountAsync(c => c.Uid == 99UL)).ShouldBe(1);
    AccessCard reloaded = (await Db.AccessCards.FindAsync(existingCardId))!;
    reloaded.BillId.ShouldBe(result.Value.BillId);
    reloaded.Deposit.ShouldBe(75m);
    reloaded.ValidUntil.ShouldBe(new DateOnly(2026, 8, 15));
    reloaded.Note.ShouldBe("new note");
    reloaded.IssuedAtUtc.ShouldBe(Clock.UtcNow);
  }

  [Fact]
  public async Task Handle_CreatesNewVehicles_LinkedToBill()
  {
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    var serviceA = Guid.NewGuid();
    var serviceB = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));
    await Db.SaveChangesAsync();
    _numbers.NextAsync(Clock.UtcNow.Year, Arg.Any<CancellationToken>()).Returns("2026/0300");

    CreateBillCommand command = MakeReservationCommand(reservationId, [guestId], []) with
    {
      NewVehicles =
      [
        new NewVehicleInput(RegistrationNumber: "VEHA01", ServiceId: serviceA),
        new NewVehicleInput(RegistrationNumber: "VEHB02", ServiceId: serviceB),
      ],
    };

    Result<CreateBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    List<Vehicle> vehicles =
      [.. Db.Vehicles.Where(v => v.BillId == result.Value.BillId).AsEnumerable().OrderBy(v => v.RegistrationNumber, StringComparer.Ordinal)];
    vehicles.Count.ShouldBe(2);
    vehicles[0].RegistrationNumber.ShouldBe("VEHA01");
    vehicles[0].ServiceId.ShouldBe(serviceA);
    vehicles[0].ReservationId.ShouldBe(reservationId);
    vehicles[0].BillId.ShouldBe(result.Value.BillId);
    vehicles[1].RegistrationNumber.ShouldBe("VEHB02");
    vehicles[1].ServiceId.ShouldBe(serviceB);
  }

  [Fact]
  public async Task Handle_LinksExistingVehicleToBill_PreservesServiceAndReservation()
  {
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();
    var vehicleId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));
    Db.Vehicles.Add(new Vehicle
    {
      Id = vehicleId,
      ReservationId = reservationId,
      BillId = null,
      ServiceId = serviceId,
      RegistrationNumber = "VEX001",
    });
    await Db.SaveChangesAsync();
    _numbers.NextAsync(Clock.UtcNow.Year, Arg.Any<CancellationToken>()).Returns("2026/0301");

    CreateBillCommand command = MakeReservationCommand(reservationId, [guestId], []) with
    {
      ExistingVehicleIds = [vehicleId],
    };

    Result<CreateBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Vehicle reloaded =
      (await Db.Vehicles.FindAsync(vehicleId))!;
    reloaded.BillId.ShouldBe(result.Value.BillId);
    reloaded.ReservationId.ShouldBe(reservationId);
    reloaded.ServiceId.ShouldBe(serviceId);
    reloaded.RegistrationNumber.ShouldBe("VEX001");
  }

  [Fact]
  public async Task Handle_Fails_WhenExistingVehicleNotFound()
  {
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));
    await Db.SaveChangesAsync();
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0302");

    CreateBillCommand command = MakeReservationCommand(reservationId, [guestId], []) with
    {
      ExistingVehicleIds = [Guid.NewGuid()],
    };

    Result<CreateBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Vehicles.NotFound");
  }

  [Fact]
  public async Task Handle_Fails_WhenExistingVehicleAlreadyLinkedToAnotherBill()
  {
    var reservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    var vehicleId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));
    Db.Vehicles.Add(new Vehicle
    {
      Id = vehicleId,
      ReservationId = reservationId,
      BillId = Guid.NewGuid(),
      ServiceId = null,
      RegistrationNumber = "VEY001",
    });
    await Db.SaveChangesAsync();
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0303");

    CreateBillCommand command = MakeReservationCommand(reservationId, [guestId], []) with
    {
      ExistingVehicleIds = [vehicleId],
    };

    Result<CreateBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Bill.VehicleAlreadyLinkedToAnotherBill");
  }

  [Fact]
  public async Task Handle_Fails_WhenExistingVehicleFromAnotherReservation()
  {
    var reservationId = Guid.NewGuid();
    var otherReservationId = Guid.NewGuid();
    var guestId = Guid.NewGuid();
    var vehicleId = Guid.NewGuid();
    Db.Guests.Add(NewGuest(guestId, reservationId));
    Db.Vehicles.Add(new Vehicle
    {
      Id = vehicleId,
      ReservationId = otherReservationId,
      BillId = null,
      ServiceId = null,
      RegistrationNumber = "VEZ001",
    });
    await Db.SaveChangesAsync();
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0304");

    CreateBillCommand command = MakeReservationCommand(reservationId, [guestId], []) with
    {
      ExistingVehicleIds = [vehicleId],
    };

    Result<CreateBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Bill.VehicleNotInReservation");
  }

  private async Task<Guid> SeedServiceWithVatRate(decimal rate)
  {
    var vatRateId = Guid.NewGuid();
    Db.VatRates.Add(new VatRate
    {
      Id = vatRateId,
      Name = $"VAT {rate}%",
      Rate = rate,
      IsActive = true,
    });
    var serviceId = Guid.NewGuid();
    Db.Services.Add(new Service
    {
      Id = serviceId,
      ServiceGroup = ServiceGroup.Others,
      ServiceTypeId = Guid.NewGuid(),
      VatRateId = vatRateId,
      Name = "Test service",
      BasePrice = 100m,
      IsActive = true,
    });
    await Db.SaveChangesAsync();
    return serviceId;
  }

  [Fact]
  public async Task Handle_DerivesVatRate_FromServiceCatalogue_WhenServiceIdProvided()
  {
    Guid serviceId = await SeedServiceWithVatRate(21m);
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0500");

    var command = new CreateBillCommand(
      null,
      new DateOnly(2026, 4, 22),
      new DateOnly(2026, 4, 23),
      new BillPayerInput("J", "D", Addr()),
      null,
      PaymentType.Card,
      Guid.NewGuid(),
      [new BillItemInput(serviceId, 1u, 100m, VatRatePercentage: 0m, 0u, 0u)],
      [], [], [], [], [], [], []);

    Result<CreateBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
    BillItem item = await Db.BillItems.SingleAsync(i => i.BillId == result.Value.BillId);
    item.VatRatePercentage.ShouldBe(21m);
  }

  [Fact]
  public async Task Handle_UsesInputVatRate_ForAdHocItem_WhenServiceIdIsNull()
  {
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0501");

    var command = new CreateBillCommand(
      null,
      new DateOnly(2026, 4, 22),
      new DateOnly(2026, 4, 23),
      new BillPayerInput("J", "D", Addr()),
      null,
      PaymentType.Card,
      Guid.NewGuid(),
      [new BillItemInput(null, 1u, 100m, VatRatePercentage: 15m, 0u, 0u)],
      [], [], [], [], [], [], []);

    Result<CreateBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
    BillItem item = await Db.BillItems.SingleAsync(i => i.BillId == result.Value.BillId);
    item.VatRatePercentage.ShouldBe(15m);
  }

  [Fact]
  public async Task Handle_Fails_WhenItemReferencesUnknownService()
  {
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0502");
    var unknownServiceId = Guid.NewGuid();

    var command = new CreateBillCommand(
      null,
      new DateOnly(2026, 4, 22),
      new DateOnly(2026, 4, 23),
      new BillPayerInput("J", "D", Addr()),
      null,
      PaymentType.Card,
      Guid.NewGuid(),
      [new BillItemInput(unknownServiceId, 1u, 100m, 0m, 0u, 0u)],
      [], [], [], [], [], [], []);

    Result<CreateBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Services.NotFound");
  }

  [Fact]
  public async Task Handle_DerivedVatRate_FlowsIntoPaymentAmount()
  {
    Guid serviceId = await SeedServiceWithVatRate(21m);
    _numbers.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("2026/0503");

    // UnitPrice gross; effective qty = recapSingle × recapDay = 2×1; total = 2×500 = 1000.
    var command = new CreateBillCommand(
      null,
      new DateOnly(2026, 4, 22),
      new DateOnly(2026, 4, 23),
      new BillPayerInput("J", "D", Addr()),
      null,
      PaymentType.Card,
      Guid.NewGuid(),
      [new BillItemInput(serviceId, 2u, 500m, VatRatePercentage: 0m, RecapSingleQuantity: 2u, RecapDayQuantity: 1u)],
      [], [], [], [], [], [], []);

    Result<CreateBillResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Bill bill = (await Db.Bills.FindAsync(result.Value.BillId))!;
    bill.Payment.Amount.ShouldBe(1000m);
  }
}
