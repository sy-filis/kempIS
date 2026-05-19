using Application.Reservations.Vehicles;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Reservations.Vehicles;
using SharedKernel;

namespace Application.UnitTests.Reservations.Vehicles;

public sealed class GetVehicleByPlateQueryHandlerTests : HandlerTestBase
{
  private static readonly DateTime DefaultNowUtc = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
  private static DateOnly DefaultToday => DateOnly.FromDateTime(DefaultNowUtc);

  private GetVehicleByPlateQueryHandler CreateSut()
  {
    Clock.Set(DefaultNowUtc);
    return new GetVehicleByPlateQueryHandler(Db, Clock);
  }

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private static Bill MakeBill(Guid id, DateOnly checkOut) => new()
  {
    Id = id,
    Number = "B-" + id.ToString("N")[..6],
    Kind = BillKind.Regular,
    ReservationId = Guid.NewGuid(),
    LanguageIdGuid = Guid.NewGuid(),
    IssuedAtUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
    CheckInAt = checkOut.AddDays(-3),
    CheckOutAt = checkOut,
    Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    LegalEntity = new LegalEntity { Name = "L", Cin = "1", Tin = "1", Address = Addr() },
    Payment = new Payment(PaymentType.Cash, 100m),
  };

  private static Vehicle MakeVehicle(string plate, Guid? billId) => new()
  {
    Id = Guid.NewGuid(),
    ReservationId = Guid.NewGuid(),
    BillId = billId,
    ServiceId = Guid.NewGuid(),
    RegistrationNumber = plate,
  };

  private async Task SeedBilledVehicleAsync(string plate, DateOnly checkOut)
  {
    Bill bill = MakeBill(Guid.NewGuid(), checkOut);
    Db.Bills.Add(bill);
    Db.Vehicles.Add(MakeVehicle(plate, bill.Id));
    await Db.SaveChangesAsync();
  }

  [Fact]
  public async Task Handle_Match_ReturnsNormalizedPlateAndCheckOutAt()
  {
    DateOnly checkOut = DefaultToday.AddDays(2);
    await SeedBilledVehicleAsync("1AB-2345", checkOut);

    Result<VehicleLookupResponse> result = await CreateSut()
      .Handle(new GetVehicleByPlateQuery("1ab 2345"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.LicencePlate.ShouldBe("1AB2345");
    result.Value.CheckoutAt.ShouldBe(checkOut);
  }

  [Fact]
  public async Task Handle_TodayIsCheckoutDay_ReturnsMatch()
  {
    await SeedBilledVehicleAsync("XYZ123", DefaultToday);

    Result<VehicleLookupResponse> result = await CreateSut()
      .Handle(new GetVehicleByPlateQuery("xyz123"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.CheckoutAt.ShouldBe(DefaultToday);
  }

  [Fact]
  public async Task Handle_BillExpired_Returns404()
  {
    await SeedBilledVehicleAsync("1AB2345", DefaultToday.AddDays(-1));

    Result<VehicleLookupResponse> result = await CreateSut()
      .Handle(new GetVehicleByPlateQuery("1AB2345"), CancellationToken.None);

    result.IsSuccess.ShouldBeFalse();
    result.Error.Type.ShouldBe(ErrorType.NotFound);
  }

  [Fact]
  public async Task Handle_VehicleWithoutBill_Returns404()
  {
    Db.Vehicles.Add(MakeVehicle("1AB2345", billId: null));
    await Db.SaveChangesAsync();

    Result<VehicleLookupResponse> result = await CreateSut()
      .Handle(new GetVehicleByPlateQuery("1AB2345"), CancellationToken.None);

    result.IsSuccess.ShouldBeFalse();
    result.Error.Type.ShouldBe(ErrorType.NotFound);
  }

  [Fact]
  public async Task Handle_NoMatchingPlate_Returns404()
  {
    await SeedBilledVehicleAsync("1AB2345", DefaultToday.AddDays(1));

    Result<VehicleLookupResponse> result = await CreateSut()
      .Handle(new GetVehicleByPlateQuery("9XX9999"), CancellationToken.None);

    result.IsSuccess.ShouldBeFalse();
    result.Error.Type.ShouldBe(ErrorType.NotFound);
  }

  [Fact]
  public async Task Handle_TwoMatches_ReturnsLatestCheckoutAt()
  {
    DateOnly earlier = DefaultToday.AddDays(1);
    DateOnly later = DefaultToday.AddDays(5);
    await SeedBilledVehicleAsync("1AB2345", earlier);
    await SeedBilledVehicleAsync("1AB-2345", later);

    Result<VehicleLookupResponse> result = await CreateSut()
      .Handle(new GetVehicleByPlateQuery("1AB2345"), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.CheckoutAt.ShouldBe(later);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public async Task Handle_NullOrEmptyPlate_ReturnsValidationProblem(string? plate)
  {
    Result<VehicleLookupResponse> result = await CreateSut()
      .Handle(new GetVehicleByPlateQuery(plate!), CancellationToken.None);

    result.IsSuccess.ShouldBeFalse();
    result.Error.Type.ShouldBe(ErrorType.Problem);
  }

  [Fact]
  public async Task Handle_TooLong_ReturnsValidationProblem()
  {
    string plate = new('A', 41);

    Result<VehicleLookupResponse> result = await CreateSut()
      .Handle(new GetVehicleByPlateQuery(plate), CancellationToken.None);

    result.IsSuccess.ShouldBeFalse();
    result.Error.Type.ShouldBe(ErrorType.Problem);
  }

  [Fact]
  public async Task Handle_NormalizesToEmpty_ReturnsValidationProblem()
  {
    Result<VehicleLookupResponse> result = await CreateSut()
      .Handle(new GetVehicleByPlateQuery("---"), CancellationToken.None);

    result.IsSuccess.ShouldBeFalse();
    result.Error.Type.ShouldBe(ErrorType.Problem);
  }
}
