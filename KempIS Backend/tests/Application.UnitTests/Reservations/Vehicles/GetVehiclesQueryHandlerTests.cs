using Application.Reservations.Vehicles;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Reservations.Vehicles;
using SharedKernel;

namespace Application.UnitTests.Reservations.Vehicles;

public sealed class GetVehiclesQueryHandlerTests : HandlerTestBase
{
  private GetVehiclesQueryHandler CreateSut() => new(Db);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private static Bill MakeBill(Guid id, DateOnly checkIn, DateOnly checkOut) => new()
  {
    Id = id,
    Number = "B-" + id.ToString("N")[..6],
    Kind = BillKind.Regular,
    ReservationId = Guid.NewGuid(),
    LanguageIdGuid = Guid.NewGuid(),
    IssuedAtUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
    CheckInAt = checkIn,
    CheckOutAt = checkOut,
    Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    LegalEntity = new LegalEntity { Name = "L", Cin = "1", Tin = "1", Address = Addr() },
    Payment = new Payment(PaymentType.Cash, 100m),
  };

  private static Vehicle MakeVehicle(Guid? billId, string plate = "1AB2345") => new()
  {
    Id = Guid.NewGuid(),
    ReservationId = Guid.NewGuid(),
    BillId = billId,
    ServiceId = Guid.NewGuid(),
    RegistrationNumber = plate,
  };

  [Fact]
  public async Task Handle_NoVehicles_ReturnsEmpty()
  {
    Result<List<VehicleResponse>> result = await CreateSut()
      .Handle(new GetVehiclesQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_VehicleWithoutBill_IsExcluded()
  {
    Db.Vehicles.Add(MakeVehicle(billId: null));
    await Db.SaveChangesAsync();

    Result<List<VehicleResponse>> result = await CreateSut()
      .Handle(new GetVehiclesQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_BillOverlapsRange_VehicleIsIncluded()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    Db.Bills.Add(bill);
    Db.Vehicles.Add(MakeVehicle(bill.Id));
    await Db.SaveChangesAsync();

    Result<List<VehicleResponse>> result = await CreateSut()
      .Handle(new GetVehiclesQuery(new DateOnly(2026, 5, 12), new DateOnly(2026, 5, 20), null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_BillOutsideRange_VehicleIsExcluded()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    Db.Bills.Add(bill);
    Db.Vehicles.Add(MakeVehicle(bill.Id));
    await Db.SaveChangesAsync();

    Result<List<VehicleResponse>> result = await CreateSut()
      .Handle(new GetVehiclesQuery(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_BillBoundaryDayTouches_VehicleIsIncluded()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    Db.Bills.Add(bill);
    Db.Vehicles.Add(MakeVehicle(bill.Id));
    await Db.SaveChangesAsync();

    Result<List<VehicleResponse>> result = await CreateSut()
      .Handle(new GetVehiclesQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 10), null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_SearchMatchesPlate_VehicleIsIncluded()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    Db.Bills.Add(bill);
    Db.Vehicles.Add(MakeVehicle(bill.Id, plate: "1AB2345"));
    Db.Vehicles.Add(MakeVehicle(bill.Id, plate: "9XY9999"));
    await Db.SaveChangesAsync();

    Result<List<VehicleResponse>> result = await CreateSut()
      .Handle(new GetVehiclesQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), "1ab"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
    result.Value[0].RegistrationNumber.ShouldBe("1AB2345");
  }

  [Fact]
  public async Task Handle_SearchMisses_ReturnsEmpty()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    Db.Bills.Add(bill);
    Db.Vehicles.Add(MakeVehicle(bill.Id, plate: "1AB2345"));
    await Db.SaveChangesAsync();

    Result<List<VehicleResponse>> result = await CreateSut()
      .Handle(new GetVehiclesQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), "zzz"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_SearchIsCaseInsensitive()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    Db.Bills.Add(bill);
    Db.Vehicles.Add(MakeVehicle(bill.Id, plate: "1ab2345"));
    await Db.SaveChangesAsync();

    Result<List<VehicleResponse>> result = await CreateSut()
      .Handle(new GetVehiclesQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), "1AB"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_SearchWhitespaceOnly_TreatedAsNoSearch()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    Db.Bills.Add(bill);
    Db.Vehicles.Add(MakeVehicle(bill.Id, plate: "1AB2345"));
    Db.Vehicles.Add(MakeVehicle(bill.Id, plate: "9XY9999"));
    await Db.SaveChangesAsync();

    Result<List<VehicleResponse>> result = await CreateSut()
      .Handle(new GetVehiclesQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), "   "), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(2);
  }

  [Fact]
  public async Task Handle_SearchAppliedAfterBillFilter_VehicleOutsideDatesExcluded()
  {
    Bill insideBill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    Bill outsideBill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5));
    Db.Bills.AddRange(insideBill, outsideBill);
    Db.Vehicles.Add(MakeVehicle(insideBill.Id, plate: "1AB2345"));
    Db.Vehicles.Add(MakeVehicle(outsideBill.Id, plate: "1AB2345"));
    await Db.SaveChangesAsync();

    Result<List<VehicleResponse>> result = await CreateSut()
      .Handle(new GetVehiclesQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), "1ab"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(1);
  }
}
