using Application.Finance.Bills.GetBillById;
using Domain.Common;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.Invoices;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Operations.AccessCards;
using Domain.Reservations.Guests;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.Vehicles;
using SharedKernel;

namespace Application.UnitTests.Finance.Bills;

public sealed class GetBillByIdQueryHandlerTests : HandlerTestBase
{
  private GetBillByIdQueryHandler CreateSut() => new(Db);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private async Task<(Guid VehicleId, Guid SpotItemId)> SeedFullBillGraph(Guid billId)
  {
    var reservationId = Guid.NewGuid();
    var invoiceId = Guid.NewGuid();
    var vehicleId = Guid.NewGuid();
    var spotItemId = Guid.NewGuid();
    var vehicleServiceId = Guid.NewGuid();
    var spotId = Guid.NewGuid();

    Db.Invoices.Add(new Invoice
    {
      Id = invoiceId,
      ReservationId = reservationId,
      Status = InvoiceStatus.Paid,
      Number = "EXT-9",
      IssuedAt = DateOnly.FromDateTime(DateTime.UtcNow),
      PaidAt = DateOnly.FromDateTime(DateTime.UtcNow),
      LinkedBillId = billId,
      Email = "seed@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    });

    Db.Bills.Add(new Bill
    {
      Id = billId,
      Number = "2026/0001",
      Kind = BillKind.Regular,
      ReservationId = reservationId,
      LanguageIdGuid = Guid.NewGuid(),
      IssuedAtUtc = DateTime.UtcNow,
      CheckInAt = new DateOnly(2026, 4, 20),
      CheckOutAt = new DateOnly(2026, 4, 22),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = Addr() },
      LegalEntity = new LegalEntity { Name = "Acme", Cin = "123", Tin = "CZ123", Address = Addr() },
      Payment = new Payment(PaymentType.Card, 1000m),
    });

    Db.BillItems.Add(new BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = Guid.NewGuid(),
      Quantity = 2u,
      UnitPrice = 500m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 1u,
      RecapDayQuantity = 2u,
    });

    Db.Bills.Add(new Bill
    {
      Id = Guid.NewGuid(),
      Number = "2026/0002",
      Kind = BillKind.Repair,
      OriginalBillId = billId,
      ReservationId = reservationId,
      LanguageIdGuid = Guid.NewGuid(),
      IssuedAtUtc = DateTime.UtcNow,
      CheckInAt = new DateOnly(2026, 4, 20),
      CheckOutAt = new DateOnly(2026, 4, 22),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = Addr() },
      LegalEntity = new LegalEntity { Name = "Acme", Cin = "123", Tin = "CZ123", Address = Addr() },
      Payment = new Payment(PaymentType.Cash, 100m),
    });

    Db.Guests.Add(new Guest
    {
      Id = Guid.NewGuid(),
      ReservationId = reservationId,
      BillId = billId,
      FirstName = "Jane",
      LastName = "Smith",
      NationalityId = Guid.NewGuid(),
      DateOfBirth = new DateOnly(1990, 1, 1),
      DocumentType = DocumentType.IdCard,
      DocumentNumber = "D1",
      Address = Addr(),
      ReasonOfStay = "Holiday",
      StayDateRange = new DateRange(new DateOnly(2026, 4, 20), new DateOnly(2026, 4, 22)),
    });

    Db.Vehicles.Add(new Vehicle
    {
      Id = vehicleId,
      ReservationId = reservationId,
      BillId = billId,
      ServiceId = vehicleServiceId,
      RegistrationNumber = "1AB 2345",
    });

    Db.ReservationSpotItems.Add(new ReservationSpotItem
    {
      Id = spotItemId,
      ReservationId = reservationId,
      SpotGroupId = Guid.NewGuid(),
      SpotId = spotId,
      HasGivenKey = true,
      HasReturnedKeys = false,
      BillId = billId,
    });

    Db.AccessCards.Add(new AccessCard
    {
      Id = Guid.NewGuid(),
      Uid = 7777UL,
      BillId = billId,
      Deposit = 250m,
      IssuedAtUtc = new DateTime(2026, 4, 21, 9, 0, 0, DateTimeKind.Utc),
      Note = "Bungalow A",
    });

    await Db.SaveChangesAsync();

    return (vehicleId, spotItemId);
  }

  [Fact]
  public async Task Handle_ReturnsBillWithAllCollections()
  {
    var billId = Guid.NewGuid();
    (Guid vehicleId, Guid spotItemId) = await SeedFullBillGraph(billId);

    Result<GetBillByIdResponse> result = await CreateSut()
      .Handle(new GetBillByIdQuery(billId), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Id.ShouldBe(billId);
    result.Value.Number.ShouldBe("2026/0001");
    result.Value.Kind.ShouldBe(BillKind.Regular);
    result.Value.Items.Count.ShouldBe(1);
    result.Value.Deductions.Count.ShouldBe(1);
    result.Value.Deductions[0].InvoiceNumber.ShouldBe("EXT-9");
    result.Value.Repairs.Count.ShouldBe(1);
    result.Value.Repairs[0].Number.ShouldBe("2026/0002");
    result.Value.Guests.Count.ShouldBe(1);
    result.Value.Guests[0].FirstName.ShouldBe("Jane");
    result.Value.Guests[0].LastName.ShouldBe("Smith");
    result.Value.Guests[0].DocumentType.ShouldBe(DocumentType.IdCard);
    result.Value.Guests[0].DocumentNumber.ShouldBe("D1");
    result.Value.Guests[0].ReasonOfStay.ShouldBe("Holiday");
    result.Value.Guests[0].StayDateRange.ShouldNotBeNull();
    result.Value.Guests[0].HasSignature.ShouldBeFalse();

    result.Value.Vehicles.Count.ShouldBe(1);
    result.Value.Vehicles[0].Id.ShouldBe(vehicleId);
    result.Value.Vehicles[0].RegistrationNumber.ShouldBe("1AB 2345");
    result.Value.Vehicles[0].ServiceId.ShouldNotBeNull();

    result.Value.SpotItems.Count.ShouldBe(1);
    result.Value.SpotItems[0].Id.ShouldBe(spotItemId);
    result.Value.SpotItems[0].SpotId.ShouldNotBeNull();
    result.Value.SpotItems[0].HasGivenKey.ShouldBeTrue();
    result.Value.SpotItems[0].HasReturnedKeys.ShouldBeFalse();

    result.Value.AccessCards.Count.ShouldBe(1);
    result.Value.AccessCards[0].Uid.ShouldBe(7777UL);
    result.Value.AccessCards[0].Deposit.ShouldBe(250m);
    result.Value.AccessCards[0].Note.ShouldBe("Bungalow A");
  }

  [Fact]
  public async Task Handle_ReturnsEmptyVehicleAndSpotItemArrays_WhenNoneLinked()
  {
    var billId = Guid.NewGuid();

    Db.Bills.Add(new Bill
    {
      Id = billId,
      Number = "2026/0003",
      Kind = BillKind.Regular,
      LanguageIdGuid = Guid.NewGuid(),
      IssuedAtUtc = DateTime.UtcNow,
      CheckInAt = new DateOnly(2026, 4, 20),
      CheckOutAt = new DateOnly(2026, 4, 22),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = Addr() },
      Payment = new Payment(PaymentType.Card, 0m),
    });

    await Db.SaveChangesAsync();

    Result<GetBillByIdResponse> result = await CreateSut()
      .Handle(new GetBillByIdQuery(billId), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Vehicles.ShouldBeEmpty();
    result.Value.SpotItems.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_ReturnsNotFound_WhenMissing()
  {
    Result<GetBillByIdResponse> result = await CreateSut()
      .Handle(new GetBillByIdQuery(Guid.NewGuid()), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Bill.NotFound");
  }

  [Fact]
  public async Task Handle_ProjectsRepairReason_OnRepairBillAndOnSummaryEntries()
  {
    var originalId = Guid.NewGuid();
    var repairId = Guid.NewGuid();
    var reservationId = Guid.NewGuid();

    Db.Bills.Add(new Bill
    {
      Id = originalId,
      Number = "2026/3001",
      Kind = BillKind.Regular,
      ReservationId = reservationId,
      LanguageIdGuid = Guid.NewGuid(),
      IssuedAtUtc = DateTime.UtcNow,
      CheckInAt = new DateOnly(2026, 4, 20),
      CheckOutAt = new DateOnly(2026, 4, 22),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = Addr() },
      LegalEntity = new LegalEntity { Name = "Acme", Cin = "123", Tin = "CZ123", Address = Addr() },
      Payment = new Payment(PaymentType.Card, 1000m),
    });

    Db.Bills.Add(new Bill
    {
      Id = repairId,
      Number = "2026/3002",
      Kind = BillKind.Repair,
      OriginalBillId = originalId,
      ReservationId = reservationId,
      LanguageIdGuid = Guid.NewGuid(),
      IssuedAtUtc = DateTime.UtcNow,
      CheckInAt = new DateOnly(2026, 4, 20),
      CheckOutAt = new DateOnly(2026, 4, 22),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = Addr() },
      LegalEntity = new LegalEntity { Name = "Acme", Cin = "123", Tin = "CZ123", Address = Addr() },
      Payment = new Payment(PaymentType.Cash, -200m),
      RepairReason = "Charged wrong nightly rate",
    });

    await Db.SaveChangesAsync();

    Result<GetBillByIdResponse> repairView =
      await CreateSut().Handle(new GetBillByIdQuery(repairId), CancellationToken.None);
    repairView.IsSuccess.ShouldBeTrue();
    repairView.Value.RepairReason.ShouldBe("Charged wrong nightly rate");

    Result<GetBillByIdResponse> originalView =
      await CreateSut().Handle(new GetBillByIdQuery(originalId), CancellationToken.None);
    originalView.IsSuccess.ShouldBeTrue();
    BillRepairSummary summary = originalView.Value.Repairs.ShouldHaveSingleItem();
    summary.Reason.ShouldBe("Charged wrong nightly rate");
  }
}
