using Application.Finance.Queries.Stats.GetServiceRevenueStats;
using Domain.Common;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Services.Services;
using SharedKernel;

namespace Application.UnitTests.Finance.Queries.Stats.GetServiceRevenueStats;

public sealed class GetServiceRevenueStatsQueryHandlerTests : HandlerTestBase
{
  private static readonly DateOnly From = new(2026, 6, 1);
  private static readonly DateOnly To = new(2026, 8, 31);
  // UnitPrice is VAT-inclusive (gross). Same UnitPrice across both VAT rates
  // makes Gross identical (=100), so OrderByDescending(Gross).ThenBy(Vat) sorts
  // the rows by ascending VAT rate.
  private static readonly decimal[] TwoVatRates = [15m, 21m];

  private GetServiceRevenueStatsQueryHandler CreateSut() => new(Db);

  private static Bill MakeBill(Guid id, DateTime issuedAtUtc, BillKind kind = BillKind.Regular) => new()
  {
    Id = id,
    Number = "B-" + id.ToString("N")[..6],
    ReservationId = Guid.NewGuid(),
    Kind = kind,
    IssuedAtUtc = issuedAtUtc,
    CheckInAt = new DateOnly(2026, 6, 1),
    CheckOutAt = new DateOnly(2026, 6, 2),
    LanguageIdGuid = Guid.NewGuid(),
    Payer = new Payer
    {
      Name = "J",
      Surname = "N",
      Address = new Address(Guid.NewGuid(), "P", "10000", "S", "1"),
    },
    LegalEntity = new LegalEntity
    {
      Name = "Acme",
      Address = new Address(Guid.NewGuid(), "P", "10000", "S", "1"),
      Cin = "12345678",
      Tin = "CZ12345678",
    },
    Payment = new Payment(PaymentType.Cash, 0m),
  };

  private static Service MakeService(Guid id, ServiceGroup group, string name, bool isActive = true) => new()
  {
    Id = id,
    Name = name,
    ServiceGroup = group,
    ServiceTypeId = Guid.NewGuid(),
    VatRateId = Guid.NewGuid(),
    BasePrice = 0m,
    IsActive = isActive,
  };

  private static BillItem MakeItem(Guid billId, Guid serviceId, uint qty, decimal unitPrice, decimal vatPct) => new()
  {
    Id = Guid.NewGuid(),
    BillId = billId,
    ServiceId = serviceId,
    Quantity = qty,
    UnitPrice = unitPrice,
    VatRatePercentage = vatPct,
    RecapSingleQuantity = qty,
    RecapDayQuantity = 1,
  };

  [Fact]
  public async Task Handle_EmptyDb_ReturnsZeros()
  {
    Result<ServiceRevenueStatsResponse> result =
      await CreateSut().Handle(new GetServiceRevenueStatsQuery(From, To), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.TotalNet.ShouldBe(0m);
    result.Value.TotalVat.ShouldBe(0m);
    result.Value.TotalGross.ShouldBe(0m);
    result.Value.Groups.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_TwoServicesInDifferentGroups_GroupsAndTotalsMatch()
  {
    var billId = Guid.NewGuid();
    var spotsServiceId = Guid.NewGuid();
    var mealsServiceId = Guid.NewGuid();
    Db.Services.Add(MakeService(spotsServiceId, ServiceGroup.Spots, "Pitch"));
    Db.Services.Add(MakeService(mealsServiceId, ServiceGroup.Meals, "Breakfast"));
    Db.Bills.Add(MakeBill(billId, new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc)));
    Db.BillItems.Add(MakeItem(billId, spotsServiceId, qty: 4, unitPrice: 100m, vatPct: 21m));
    Db.BillItems.Add(MakeItem(billId, mealsServiceId, qty: 2, unitPrice: 50m, vatPct: 12m));
    await Db.SaveChangesAsync();

    Result<ServiceRevenueStatsResponse> result =
      await CreateSut().Handle(new GetServiceRevenueStatsQuery(From, To), CancellationToken.None);

    // UnitPrice is VAT-inclusive (gross). Per-row gross = qty × UnitPrice; net
    // is backed out of gross via gross / (1 + VAT/100).
    decimal netSpots = Math.Round(400m / 1.21m, 2, MidpointRounding.AwayFromZero); // 4 × 100 @ 21%
    decimal netMeals = Math.Round(100m / 1.12m, 2, MidpointRounding.AwayFromZero); // 2 × 50  @ 12%
    result.Value.TotalGross.ShouldBe(400m + 100m);
    result.Value.TotalNet.ShouldBe(netSpots + netMeals);
    result.Value.TotalVat.ShouldBe(result.Value.TotalGross - result.Value.TotalNet);
    result.Value.Groups.Count.ShouldBe(2);
    result.Value.Groups[0].ServiceGroup.ShouldBe(nameof(ServiceGroup.Spots));
    result.Value.Groups[0].Services.Count.ShouldBe(1);
    result.Value.Groups[1].ServiceGroup.ShouldBe(nameof(ServiceGroup.Meals));
  }

  [Fact]
  public async Task Handle_RepairBill_IsExcluded()
  {
    var billId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();
    Db.Services.Add(MakeService(serviceId, ServiceGroup.Spots, "Pitch"));
    Db.Bills.Add(MakeBill(billId, new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), BillKind.Repair));
    Db.BillItems.Add(MakeItem(billId, serviceId, qty: 1, unitPrice: 100m, vatPct: 21m));
    await Db.SaveChangesAsync();

    Result<ServiceRevenueStatsResponse> result =
      await CreateSut().Handle(new GetServiceRevenueStatsQuery(From, To), CancellationToken.None);

    result.Value.TotalNet.ShouldBe(0m);
    result.Value.Groups.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_NullServiceId_IsExcluded()
  {
    var billId = Guid.NewGuid();
    Db.Bills.Add(MakeBill(billId, new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc)));
    BillItem orphan = new()
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = null,
      Quantity = 1,
      UnitPrice = 100m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 1,
      RecapDayQuantity = 1,
    };
    Db.BillItems.Add(orphan);
    await Db.SaveChangesAsync();

    Result<ServiceRevenueStatsResponse> result =
      await CreateSut().Handle(new GetServiceRevenueStatsQuery(From, To), CancellationToken.None);

    result.Value.TotalNet.ShouldBe(0m);
    result.Value.Groups.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_SameServiceTwoVatRates_EmitsTwoRows()
  {
    var billA = Guid.NewGuid();
    var billB = Guid.NewGuid();
    var serviceId = Guid.NewGuid();
    Db.Services.Add(MakeService(serviceId, ServiceGroup.Spots, "Pitch"));
    Db.Bills.Add(MakeBill(billA, new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc)));
    Db.Bills.Add(MakeBill(billB, new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc)));
    Db.BillItems.Add(MakeItem(billA, serviceId, qty: 1, unitPrice: 100m, vatPct: 15m));
    Db.BillItems.Add(MakeItem(billB, serviceId, qty: 1, unitPrice: 100m, vatPct: 21m));
    await Db.SaveChangesAsync();

    Result<ServiceRevenueStatsResponse> result =
      await CreateSut().Handle(new GetServiceRevenueStatsQuery(From, To), CancellationToken.None);

    result.Value.Groups.Count.ShouldBe(1);
    result.Value.Groups[0].Services.Count.ShouldBe(2);
    result.Value.Groups[0].Services.Select(s => s.VatRatePercentage).ShouldBe(TwoVatRates);
  }

  [Fact]
  public async Task Handle_InactiveServiceWithSales_IsIncluded()
  {
    var billId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();
    Db.Services.Add(MakeService(serviceId, ServiceGroup.Others, "Discontinued", isActive: false));
    Db.Bills.Add(MakeBill(billId, new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc)));
    Db.BillItems.Add(MakeItem(billId, serviceId, qty: 1, unitPrice: 50m, vatPct: 21m));
    await Db.SaveChangesAsync();

    Result<ServiceRevenueStatsResponse> result =
      await CreateSut().Handle(new GetServiceRevenueStatsQuery(From, To), CancellationToken.None);

    result.Value.Groups.Count.ShouldBe(1);
    result.Value.Groups[0].Services.Count.ShouldBe(1);
    result.Value.Groups[0].Services[0].IsActive.ShouldBeFalse();
  }

  [Fact]
  public async Task Handle_BillIssuedExactlyOnBoundary_IsIncluded()
  {
    var bill1 = Guid.NewGuid();
    var bill2 = Guid.NewGuid();
    var serviceId = Guid.NewGuid();
    Db.Services.Add(MakeService(serviceId, ServiceGroup.Spots, "Pitch"));
    Db.Bills.Add(MakeBill(bill1, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)));
    Db.Bills.Add(MakeBill(bill2, new DateTime(2026, 8, 31, 23, 59, 59, DateTimeKind.Utc)));
    Db.BillItems.Add(MakeItem(bill1, serviceId, qty: 1, unitPrice: 10m, vatPct: 0m));
    Db.BillItems.Add(MakeItem(bill2, serviceId, qty: 1, unitPrice: 10m, vatPct: 0m));
    await Db.SaveChangesAsync();

    Result<ServiceRevenueStatsResponse> result =
      await CreateSut().Handle(new GetServiceRevenueStatsQuery(From, To), CancellationToken.None);

    result.Value.TotalNet.ShouldBe(20m);
  }

  [Fact]
  public async Task Handle_GrossUsesRecapSingleTimesRecapDayTimesUnitPrice()
  {
    var billId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();
    Db.Services.Add(MakeService(serviceId, ServiceGroup.Spots, "Pitch"));
    Db.Bills.Add(MakeBill(billId, new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc)));
    // recapSingle (2) × recapDay (3) × unitPrice (100) = 600. The legacy
    // Quantity * UnitPrice formula would yield 100 - pin against 600.
    Db.BillItems.Add(new BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = serviceId,
      Quantity = 1,
      UnitPrice = 100m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 2,
      RecapDayQuantity = 3,
    });
    await Db.SaveChangesAsync();

    Result<ServiceRevenueStatsResponse> result =
      await CreateSut().Handle(new GetServiceRevenueStatsQuery(From, To), CancellationToken.None);

    result.Value.TotalGross.ShouldBe(600m);
  }

  [Fact]
  public async Task Handle_BillOutsideRange_IsExcluded()
  {
    var billId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();
    Db.Services.Add(MakeService(serviceId, ServiceGroup.Spots, "Pitch"));
    Db.Bills.Add(MakeBill(billId, new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc)));
    Db.BillItems.Add(MakeItem(billId, serviceId, qty: 1, unitPrice: 100m, vatPct: 21m));
    await Db.SaveChangesAsync();

    Result<ServiceRevenueStatsResponse> result =
      await CreateSut().Handle(new GetServiceRevenueStatsQuery(From, To), CancellationToken.None);

    result.Value.Groups.ShouldBeEmpty();
  }
}
