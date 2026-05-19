using Application.Finance.Queries.Stats.GetRevenueByPaymentMethod;
using Domain.Common;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Services.Services;
using SharedKernel;

namespace Application.UnitTests.Finance.Queries.Stats.GetRevenueByPaymentMethod;

public sealed class GetRevenueByPaymentMethodQueryHandlerTests : HandlerTestBase
{
  private static readonly DateOnly From = new(2026, 6, 1);
  private static readonly DateOnly To = new(2026, 8, 31);

  private GetRevenueByPaymentMethodQueryHandler CreateSut() => new(Db);

  private static Bill MakeBill(Guid id, DateTime issuedAtUtc, PaymentType payment, BillKind kind = BillKind.Regular) => new()
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
    Payment = new Payment(payment, 0m),
  };

  private static BillItem MakeItem(Guid billId, uint qty, decimal unitPrice, decimal vatPct) => new()
  {
    Id = Guid.NewGuid(),
    BillId = billId,
    ServiceId = Guid.NewGuid(),
    Quantity = qty,
    UnitPrice = unitPrice,
    VatRatePercentage = vatPct,
    RecapSingleQuantity = qty,
    RecapDayQuantity = 1,
  };

  [Fact]
  public async Task Handle_EmptyDb_ReturnsBothRowsWithZeros()
  {
    Result<RevenueByPaymentMethodResponse> result =
      await CreateSut().Handle(new GetRevenueByPaymentMethodQuery(From, To), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.TotalBillCount.ShouldBe(0);
    result.Value.TotalGross.ShouldBe(0m);
    result.Value.Rows.Count.ShouldBe(2);
    result.Value.Rows.ShouldAllBe(r => r.Gross == 0m && r.BillCount == 0 && r.SharePercent == 0m);
  }

  [Fact]
  public async Task Handle_TwoBills_DifferentMethods_BothRowsPopulated()
  {
    var cashBill = Guid.NewGuid();
    var cardBill = Guid.NewGuid();
    Db.Bills.Add(MakeBill(cashBill, new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), PaymentType.Cash));
    Db.Bills.Add(MakeBill(cardBill, new DateTime(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc), PaymentType.Card));
    // UnitPrice is VAT-inclusive (gross). Row gross = qty × UnitPrice.
    Db.BillItems.Add(MakeItem(cashBill, qty: 1, unitPrice: 100m, vatPct: 21m));  // gross = 100
    Db.BillItems.Add(MakeItem(cardBill, qty: 2, unitPrice: 50m, vatPct: 21m));   // gross = 100
    await Db.SaveChangesAsync();

    Result<RevenueByPaymentMethodResponse> result =
      await CreateSut().Handle(new GetRevenueByPaymentMethodQuery(From, To), CancellationToken.None);

    result.Value.TotalBillCount.ShouldBe(2);
    result.Value.TotalGross.ShouldBe(200m);
    result.Value.Rows.First(r => r.PaymentType == nameof(PaymentType.Cash)).Gross.ShouldBe(100m);
    result.Value.Rows.First(r => r.PaymentType == nameof(PaymentType.Card)).Gross.ShouldBe(100m);
    result.Value.Rows.Sum(r => r.SharePercent).ShouldBe(100m);
  }

  [Fact]
  public async Task Handle_OneBillWithMultipleItems_CountsBillOnce()
  {
    var billId = Guid.NewGuid();
    Db.Bills.Add(MakeBill(billId, new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), PaymentType.Cash));
    Db.BillItems.Add(MakeItem(billId, qty: 1, unitPrice: 50m, vatPct: 21m));
    Db.BillItems.Add(MakeItem(billId, qty: 1, unitPrice: 50m, vatPct: 21m));
    await Db.SaveChangesAsync();

    Result<RevenueByPaymentMethodResponse> result =
      await CreateSut().Handle(new GetRevenueByPaymentMethodQuery(From, To), CancellationToken.None);

    result.Value.TotalBillCount.ShouldBe(1);
    result.Value.Rows.First(r => r.PaymentType == nameof(PaymentType.Cash)).BillCount.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_RepairBill_IsExcluded()
  {
    var billId = Guid.NewGuid();
    Db.Bills.Add(MakeBill(billId, new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), PaymentType.Cash, BillKind.Repair));
    Db.BillItems.Add(MakeItem(billId, qty: 1, unitPrice: 100m, vatPct: 21m));
    await Db.SaveChangesAsync();

    Result<RevenueByPaymentMethodResponse> result =
      await CreateSut().Handle(new GetRevenueByPaymentMethodQuery(From, To), CancellationToken.None);

    result.Value.TotalGross.ShouldBe(0m);
  }

  [Fact]
  public async Task Handle_BillOnBoundaryDays_IsIncluded()
  {
    var bill1 = Guid.NewGuid();
    var bill2 = Guid.NewGuid();
    Db.Bills.Add(MakeBill(bill1, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), PaymentType.Cash));
    Db.Bills.Add(MakeBill(bill2, new DateTime(2026, 8, 31, 23, 59, 59, DateTimeKind.Utc), PaymentType.Card));
    Db.BillItems.Add(MakeItem(bill1, qty: 1, unitPrice: 10m, vatPct: 0m));
    Db.BillItems.Add(MakeItem(bill2, qty: 1, unitPrice: 10m, vatPct: 0m));
    await Db.SaveChangesAsync();

    Result<RevenueByPaymentMethodResponse> result =
      await CreateSut().Handle(new GetRevenueByPaymentMethodQuery(From, To), CancellationToken.None);

    result.Value.TotalGross.ShouldBe(20m);
  }

  [Fact]
  public async Task Handle_BillOutsideRange_IsExcluded()
  {
    var billId = Guid.NewGuid();
    Db.Bills.Add(MakeBill(billId, new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc), PaymentType.Cash));
    Db.BillItems.Add(MakeItem(billId, qty: 1, unitPrice: 100m, vatPct: 0m));
    await Db.SaveChangesAsync();

    Result<RevenueByPaymentMethodResponse> result =
      await CreateSut().Handle(new GetRevenueByPaymentMethodQuery(From, To), CancellationToken.None);

    result.Value.TotalGross.ShouldBe(0m);
  }

  [Fact]
  public async Task Handle_GrossUsesRecapSingleTimesRecapDayTimesUnitPrice()
  {
    var billId = Guid.NewGuid();
    Db.Bills.Add(MakeBill(billId, new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), PaymentType.Cash));
    // recapSingle (2) × recapDay (3) × unitPrice (100) = 600. The legacy
    // Quantity * UnitPrice formula would yield 100 - pin against 600.
    Db.BillItems.Add(new BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = Guid.NewGuid(),
      Quantity = 1,
      UnitPrice = 100m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 2,
      RecapDayQuantity = 3,
    });
    await Db.SaveChangesAsync();

    Result<RevenueByPaymentMethodResponse> result =
      await CreateSut().Handle(new GetRevenueByPaymentMethodQuery(From, To), CancellationToken.None);

    result.Value.TotalGross.ShouldBe(600m);
  }

  [Fact]
  public async Task Handle_TotalGross_MatchesServiceRevenueStats()
  {
    var cashBill = Guid.NewGuid();
    var cardBill = Guid.NewGuid();
    var spotsServiceId = Guid.NewGuid();
    var mealsServiceId = Guid.NewGuid();

    Db.Services.Add(new Service
    {
      Id = spotsServiceId,
      Name = "Pitch",
      ServiceGroup = ServiceGroup.Spots,
      ServiceTypeId = Guid.NewGuid(),
      VatRateId = Guid.NewGuid(),
      BasePrice = 0m,
      IsActive = true,
    });
    Db.Services.Add(new Service
    {
      Id = mealsServiceId,
      Name = "Breakfast",
      ServiceGroup = ServiceGroup.Meals,
      ServiceTypeId = Guid.NewGuid(),
      VatRateId = Guid.NewGuid(),
      BasePrice = 0m,
      IsActive = true,
    });
    Db.Bills.Add(MakeBill(cashBill, new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), PaymentType.Cash));
    Db.Bills.Add(MakeBill(cardBill, new DateTime(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc), PaymentType.Card));
    Db.BillItems.Add(new BillItem
    {
      Id = Guid.NewGuid(),
      BillId = cashBill,
      ServiceId = spotsServiceId,
      Quantity = 4,
      UnitPrice = 100m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 4,
      RecapDayQuantity = 1,
    });
    Db.BillItems.Add(new BillItem
    {
      Id = Guid.NewGuid(),
      BillId = cardBill,
      ServiceId = mealsServiceId,
      Quantity = 2,
      UnitPrice = 50m,
      VatRatePercentage = 12m,
      RecapSingleQuantity = 2,
      RecapDayQuantity = 1,
    });
    await Db.SaveChangesAsync();

    Result<RevenueByPaymentMethodResponse> revenueResult =
      await CreateSut().Handle(new GetRevenueByPaymentMethodQuery(From, To), CancellationToken.None);

    Application.Finance.Queries.Stats.GetServiceRevenueStats.GetServiceRevenueStatsQueryHandler servicesSut = new(Db);
    Result<Application.Finance.Queries.Stats.GetServiceRevenueStats.ServiceRevenueStatsResponse> servicesResult =
      await servicesSut.Handle(
        new Application.Finance.Queries.Stats.GetServiceRevenueStats.GetServiceRevenueStatsQuery(From, To),
        CancellationToken.None);

    revenueResult.Value.TotalGross.ShouldBe(servicesResult.Value.TotalGross);
  }
}
