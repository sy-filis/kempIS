using Application.Finance.FinancialClosings.GetFinancialClosing;
using SharedKernel;

namespace Application.UnitTests.Finance.FinancialClosings;

public sealed class GetFinancialClosingQueryHandlerTests : HandlerTestBase
{
  private GetFinancialClosingQueryHandler CreateSut() => new(Db);

  [Fact]
  public async Task Handle_ReturnsNotFound_WhenClosingMissing()
  {
    Result<FinancialClosingDetailResponse> result =
      await CreateSut().Handle(new GetFinancialClosingQuery(Guid.NewGuid()), default);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("FinancialClosings.NotFound");
  }

  [Fact]
  public async Task Handle_ReturnsEmptyDetail_WhenClosingHasNoBills()
  {
    var id = Guid.NewGuid();
    var closedAt = new DateTime(2026, 5, 11, 18, 0, 0, DateTimeKind.Utc);
    var createdBy = Guid.NewGuid();
    Db.FinancialClosings.Add(new Domain.Finance.FinancialClosings.FinancialClosing
    {
      Id = id,
      FinancialClosingId = 42u,
      ClosedAtUtc = closedAt,
      TotalAmount = 0m,
      CreatedByUserId = createdBy,
    });
    await Db.SaveChangesAsync();

    Result<FinancialClosingDetailResponse> result =
      await CreateSut().Handle(new GetFinancialClosingQuery(id), default);

    result.IsSuccess.ShouldBeTrue();
    FinancialClosingDetailResponse body = result.Value;
    body.Id.ShouldBe(id);
    body.FinancialClosingId.ShouldBe(42u);
    body.ClosedAtUtc.ShouldBe(closedAt);
    body.CreatedByUserId.ShouldBe(createdBy);
    body.Bills.ShouldBeEmpty();
    body.PaymentTotals.Cash.ShouldBe(0m);
    body.PaymentTotals.Card.ShouldBe(0m);
    body.PaymentTotals.Total.ShouldBe(0m);
    body.VatRecap.ShouldBeEmpty();
    body.VatRecapByServiceType.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_ProjectsBill_WithPayerNamePaymentTypeKindAndTotal()
  {
    var closingId = Guid.NewGuid();
    var billId = Guid.NewGuid();
    var issuedAt = new DateTime(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc);
    Db.FinancialClosings.Add(new Domain.Finance.FinancialClosings.FinancialClosing
    {
      Id = closingId,
      FinancialClosingId = 1u,
      ClosedAtUtc = issuedAt.AddHours(10),
      TotalAmount = 1138m,
    });
    Db.Bills.Add(BuildBill(
      billId,
      closingId,
      number: "2026-0042",
      issuedAtUtc: issuedAt,
      payerName: "Jan",
      payerSurname: "Novák",
      paymentType: Domain.Finance.Payments.PaymentType.Cash,
      amount: 1138m));
    await Db.SaveChangesAsync();

    Result<FinancialClosingDetailResponse> result =
      await CreateSut().Handle(new GetFinancialClosingQuery(closingId), default);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Bills.Count.ShouldBe(1);
    FinancialClosingBillItem only = result.Value.Bills[0];
    only.Id.ShouldBe(billId);
    only.Number.ShouldBe("2026-0042");
    only.IssuedAtUtc.ShouldBe(issuedAt);
    only.PayerName.ShouldBe("Jan Novák");
    only.PaymentType.ShouldBe(Domain.Finance.Payments.PaymentType.Cash);
    only.Total.ShouldBe(1138m);
    only.Kind.ShouldBe(Domain.Finance.Bills.BillKind.Regular);
    only.OriginalBillId.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_OrdersBillsByIssuedAtAscending()
  {
    var closingId = Guid.NewGuid();
    Db.FinancialClosings.Add(new Domain.Finance.FinancialClosings.FinancialClosing
    {
      Id = closingId,
      FinancialClosingId = 2u,
      ClosedAtUtc = new DateTime(2026, 5, 11, 20, 0, 0, DateTimeKind.Utc),
      TotalAmount = 100m,
    });
    DateTime earlier = new(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc);
    DateTime later = new(2026, 5, 11, 9, 0, 0, DateTimeKind.Utc);
    var earlierId = Guid.NewGuid();
    var laterId = Guid.NewGuid();
    Db.Bills.Add(BuildBill(laterId, closingId, "B-2", later, "A", "Z", Domain.Finance.Payments.PaymentType.Cash, 50m));
    Db.Bills.Add(BuildBill(earlierId, closingId, "B-1", earlier, "A", "Z", Domain.Finance.Payments.PaymentType.Cash, 50m));
    await Db.SaveChangesAsync();

    Result<FinancialClosingDetailResponse> result =
      await CreateSut().Handle(new GetFinancialClosingQuery(closingId), default);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Bills.Select(b => b.Id).ShouldBe([earlierId, laterId]);
  }

  [Fact]
  public async Task Handle_SplitsPaymentTotals_ByCashAndCard()
  {
    var closingId = Guid.NewGuid();
    Db.FinancialClosings.Add(new Domain.Finance.FinancialClosings.FinancialClosing
    {
      Id = closingId,
      FinancialClosingId = 3u,
      ClosedAtUtc = new DateTime(2026, 5, 11, 20, 0, 0, DateTimeKind.Utc),
      TotalAmount = 1380m,
    });
    DateTime t = new(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc);
    Db.Bills.Add(BuildBill(Guid.NewGuid(), closingId, "B-1", t, "A", "B", Domain.Finance.Payments.PaymentType.Cash, 1138m));
    Db.Bills.Add(BuildBill(Guid.NewGuid(), closingId, "B-2", t.AddHours(1), "C", "D", Domain.Finance.Payments.PaymentType.Card, 242m));
    await Db.SaveChangesAsync();

    Result<FinancialClosingDetailResponse> result =
      await CreateSut().Handle(new GetFinancialClosingQuery(closingId), default);

    result.IsSuccess.ShouldBeTrue();
    result.Value.PaymentTotals.Cash.ShouldBe(1138m);
    result.Value.PaymentTotals.Card.ShouldBe(242m);
    result.Value.PaymentTotals.Total.ShouldBe(1380m);
  }

  [Fact]
  public async Task Handle_GroupsByServiceTypeAndRate_InByServiceTypeRecap()
  {
    var closingId = Guid.NewGuid();
    var billId = Guid.NewGuid();
    var spotTypeId = Guid.NewGuid();
    var drinkTypeId = Guid.NewGuid();
    var spotServiceId = Guid.NewGuid();
    var drinkServiceId = Guid.NewGuid();

    Db.FinancialClosings.Add(new Domain.Finance.FinancialClosings.FinancialClosing
    {
      Id = closingId,
      FinancialClosingId = 1u,
      ClosedAtUtc = new DateTime(2026, 5, 11, 20, 0, 0, DateTimeKind.Utc),
      TotalAmount = 1138m,
    });
    Db.Bills.Add(BuildBill(billId, closingId, "B-1", new DateTime(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc),
      "A", "B", Domain.Finance.Payments.PaymentType.Cash, 1138m));
    Db.ServiceTypes.Add(new Domain.Services.ServiceTypes.ServiceType { Id = spotTypeId, Name = "Spot fees", IsActive = true });
    Db.ServiceTypes.Add(new Domain.Services.ServiceTypes.ServiceType { Id = drinkTypeId, Name = "Drinks", IsActive = true });
    Db.Services.Add(new Domain.Services.Services.Service
    {
      Id = spotServiceId,
      ServiceGroup = Domain.Services.Services.ServiceGroup.Spots,
      ServiceTypeId = spotTypeId,
      VatRateId = Guid.NewGuid(),
      Name = "Spot night",
      BasePrice = 100m,
      IsActive = true,
    });
    Db.Services.Add(new Domain.Services.Services.Service
    {
      Id = drinkServiceId,
      ServiceGroup = Domain.Services.Services.ServiceGroup.Meals,
      ServiceTypeId = drinkTypeId,
      VatRateId = Guid.NewGuid(),
      Name = "Beer",
      BasePrice = 50m,
      IsActive = true,
    });
    // 8 nights × 112 = 896 gross @ 12% → net 800.00 vat 96.00
    Db.BillItems.Add(new Domain.Finance.BillItems.BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = spotServiceId,
      Quantity = 8,
      UnitPrice = 112m,
      VatRatePercentage = 12m,
      RecapSingleQuantity = 8,
      RecapDayQuantity = 1,
    });
    // 2 × 121 = 242 gross @ 21% → net 200.00 vat 42.00
    Db.BillItems.Add(new Domain.Finance.BillItems.BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = drinkServiceId,
      Quantity = 2,
      UnitPrice = 121m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 2,
      RecapDayQuantity = 1,
    });
    await Db.SaveChangesAsync();

    Result<FinancialClosingDetailResponse> result =
      await CreateSut().Handle(new GetFinancialClosingQuery(closingId), default);

    result.IsSuccess.ShouldBeTrue();
    result.Value.VatRecapByServiceType.Count.ShouldBe(2);
    // Ordered by service-type name (Drinks before Spot fees alphabetically), then rate.
    FinancialClosingVatRecapByServiceTypeRow drinks = result.Value.VatRecapByServiceType[0];
    drinks.ServiceTypeName.ShouldBe("Drinks");
    drinks.VatRatePercentage.ShouldBe(21m);
    drinks.Gross.ShouldBe(242m);
    drinks.Net.ShouldBe(200m);
    drinks.Vat.ShouldBe(42m);
    FinancialClosingVatRecapByServiceTypeRow spots = result.Value.VatRecapByServiceType[1];
    spots.ServiceTypeName.ShouldBe("Spot fees");
    spots.VatRatePercentage.ShouldBe(12m);
    spots.Gross.ShouldBe(896m);
    spots.Net.ShouldBe(800m);
    spots.Vat.ShouldBe(96m);
  }

  [Fact]
  public async Task Handle_EmitsMultipleRows_WhenSingleServiceTypeSpansVatRates()
  {
    var closingId = Guid.NewGuid();
    var billId = Guid.NewGuid();
    var typeId = Guid.NewGuid();
    var serviceLowId = Guid.NewGuid();
    var serviceHighId = Guid.NewGuid();
    Db.FinancialClosings.Add(new Domain.Finance.FinancialClosings.FinancialClosing
    {
      Id = closingId,
      FinancialClosingId = 1u,
      ClosedAtUtc = new DateTime(2026, 5, 11, 20, 0, 0, DateTimeKind.Utc),
      TotalAmount = 1138m,
    });
    Db.Bills.Add(BuildBill(billId, closingId, "B-1", new DateTime(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc),
      "A", "B", Domain.Finance.Payments.PaymentType.Cash, 1138m));
    Db.ServiceTypes.Add(new Domain.Services.ServiceTypes.ServiceType { Id = typeId, Name = "Spot fees", IsActive = true });
    Db.Services.Add(new Domain.Services.Services.Service
    {
      Id = serviceLowId,
      ServiceGroup = Domain.Services.Services.ServiceGroup.Spots,
      ServiceTypeId = typeId,
      VatRateId = Guid.NewGuid(),
      Name = "Low-rate spot",
      BasePrice = 100m,
      IsActive = true,
    });
    Db.Services.Add(new Domain.Services.Services.Service
    {
      Id = serviceHighId,
      ServiceGroup = Domain.Services.Services.ServiceGroup.Spots,
      ServiceTypeId = typeId,
      VatRateId = Guid.NewGuid(),
      Name = "High-rate spot",
      BasePrice = 200m,
      IsActive = true,
    });
    // 896 gross @ 12%
    Db.BillItems.Add(new Domain.Finance.BillItems.BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = serviceLowId,
      Quantity = 8,
      UnitPrice = 112m,
      VatRatePercentage = 12m,
      RecapSingleQuantity = 8,
      RecapDayQuantity = 1,
    });
    // 242 gross @ 21%
    Db.BillItems.Add(new Domain.Finance.BillItems.BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = serviceHighId,
      Quantity = 2,
      UnitPrice = 121m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 2,
      RecapDayQuantity = 1,
    });
    await Db.SaveChangesAsync();

    Result<FinancialClosingDetailResponse> result =
      await CreateSut().Handle(new GetFinancialClosingQuery(closingId), default);

    result.IsSuccess.ShouldBeTrue();
    result.Value.VatRecapByServiceType.Count.ShouldBe(2);
    result.Value.VatRecapByServiceType.Select(r => r.VatRatePercentage).ShouldBe([12m, 21m]);
    result.Value.VatRecapByServiceType.All(r => r.ServiceTypeName == "Spot fees").ShouldBeTrue();
    result.Value.VatRecapByServiceType[0].Gross.ShouldBe(896m);
    result.Value.VatRecapByServiceType[1].Gross.ShouldBe(242m);
  }

  [Fact]
  public async Task Handle_AggregatesFlatVatRecap_FromByServiceTypeRows()
  {
    var closingId = Guid.NewGuid();
    var billId = Guid.NewGuid();
    var spotTypeId = Guid.NewGuid();
    var drinkTypeId = Guid.NewGuid();
    var spotServiceId = Guid.NewGuid();
    var drinkServiceId = Guid.NewGuid();

    Db.FinancialClosings.Add(new Domain.Finance.FinancialClosings.FinancialClosing
    {
      Id = closingId,
      FinancialClosingId = 1u,
      ClosedAtUtc = new DateTime(2026, 5, 11, 20, 0, 0, DateTimeKind.Utc),
      TotalAmount = 1138m,
    });
    Db.Bills.Add(BuildBill(billId, closingId, "B-1", new DateTime(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc),
      "A", "B", Domain.Finance.Payments.PaymentType.Cash, 1138m));
    Db.ServiceTypes.Add(new Domain.Services.ServiceTypes.ServiceType { Id = spotTypeId, Name = "Spot fees", IsActive = true });
    Db.ServiceTypes.Add(new Domain.Services.ServiceTypes.ServiceType { Id = drinkTypeId, Name = "Drinks", IsActive = true });
    Db.Services.Add(new Domain.Services.Services.Service
    {
      Id = spotServiceId,
      ServiceGroup = Domain.Services.Services.ServiceGroup.Spots,
      ServiceTypeId = spotTypeId,
      VatRateId = Guid.NewGuid(),
      Name = "Spot",
      BasePrice = 100m,
      IsActive = true,
    });
    Db.Services.Add(new Domain.Services.Services.Service
    {
      Id = drinkServiceId,
      ServiceGroup = Domain.Services.Services.ServiceGroup.Meals,
      ServiceTypeId = drinkTypeId,
      VatRateId = Guid.NewGuid(),
      Name = "Beer",
      BasePrice = 50m,
      IsActive = true,
    });
    // Both at 21%: 484 + 242 = 726 gross @ 21% → flat row should fold to one.
    Db.BillItems.Add(new Domain.Finance.BillItems.BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = spotServiceId,
      Quantity = 4,
      UnitPrice = 121m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 4,
      RecapDayQuantity = 1,
    });
    Db.BillItems.Add(new Domain.Finance.BillItems.BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = drinkServiceId,
      Quantity = 2,
      UnitPrice = 121m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 2,
      RecapDayQuantity = 1,
    });
    await Db.SaveChangesAsync();

    Result<FinancialClosingDetailResponse> result =
      await CreateSut().Handle(new GetFinancialClosingQuery(closingId), default);

    result.IsSuccess.ShouldBeTrue();
    result.Value.VatRecapByServiceType.Count.ShouldBe(2);
    result.Value.VatRecap.Count.ShouldBe(1);
    FinancialClosingVatRecapRow row = result.Value.VatRecap[0];
    row.VatRatePercentage.ShouldBe(21m);
    row.Gross.ShouldBe(726m);
    // Sum of per-(type, rate) nets, no re-rounding.
    row.Net.ShouldBe(result.Value.VatRecapByServiceType.Sum(r => r.Net));
    row.Vat.ShouldBe(result.Value.VatRecapByServiceType.Sum(r => r.Vat));
  }

  [Fact]
  public async Task Handle_IncludesRepairBillKindAndOriginalBillId()
  {
    var closingId = Guid.NewGuid();
    var regularId = Guid.NewGuid();
    var repairId = Guid.NewGuid();
    Db.FinancialClosings.Add(new Domain.Finance.FinancialClosings.FinancialClosing
    {
      Id = closingId,
      FinancialClosingId = 1u,
      ClosedAtUtc = new DateTime(2026, 5, 11, 20, 0, 0, DateTimeKind.Utc),
      TotalAmount = 0m,
    });
    DateTime t = new(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc);
    Db.Bills.Add(BuildBill(regularId, closingId, "B-1", t, "A", "B", Domain.Finance.Payments.PaymentType.Cash, 100m));
    Db.Bills.Add(BuildBill(repairId, closingId, "B-1-R", t.AddMinutes(1), "A", "B", Domain.Finance.Payments.PaymentType.Cash, -100m,
      kind: Domain.Finance.Bills.BillKind.Repair, originalBillId: regularId));
    await Db.SaveChangesAsync();

    Result<FinancialClosingDetailResponse> result =
      await CreateSut().Handle(new GetFinancialClosingQuery(closingId), default);

    result.IsSuccess.ShouldBeTrue();
    FinancialClosingBillItem repair = result.Value.Bills.Single(b => b.Id == repairId);
    repair.Kind.ShouldBe(Domain.Finance.Bills.BillKind.Repair);
    repair.OriginalBillId.ShouldBe(regularId);
    repair.Total.ShouldBe(-100m);
  }

  [Fact]
  public async Task Handle_VatRecapGrossUsesRecapSingleTimesRecapDayTimesUnitPrice()
  {
    var closingId = Guid.NewGuid();
    var billId = Guid.NewGuid();
    var typeId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();
    Db.FinancialClosings.Add(new Domain.Finance.FinancialClosings.FinancialClosing
    {
      Id = closingId,
      FinancialClosingId = 1u,
      ClosedAtUtc = new DateTime(2026, 5, 11, 20, 0, 0, DateTimeKind.Utc),
      TotalAmount = 600m,
    });
    Db.Bills.Add(BuildBill(billId, closingId, "B-1",
      new DateTime(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc),
      "A", "B", Domain.Finance.Payments.PaymentType.Cash, 600m));
    Db.ServiceTypes.Add(new Domain.Services.ServiceTypes.ServiceType { Id = typeId, Name = "Spots", IsActive = true });
    Db.Services.Add(new Domain.Services.Services.Service
    {
      Id = serviceId,
      ServiceGroup = Domain.Services.Services.ServiceGroup.Spots,
      ServiceTypeId = typeId,
      VatRateId = Guid.NewGuid(),
      Name = "Spot",
      BasePrice = 100m,
      IsActive = true,
    });
    // recapSingle (2) × recapDay (3) × unitPrice (100) = 600. The legacy
    // Quantity * UnitPrice formula would yield 100 - pin against 600.
    Db.BillItems.Add(new Domain.Finance.BillItems.BillItem
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

    Result<FinancialClosingDetailResponse> result =
      await CreateSut().Handle(new GetFinancialClosingQuery(closingId), default);

    result.IsSuccess.ShouldBeTrue();
    result.Value.VatRecap.Count.ShouldBe(1);
    result.Value.VatRecap[0].Gross.ShouldBe(600m);
  }

  private static Domain.Finance.Bills.Bill BuildBill(
    Guid id,
    Guid? financialClosingId,
    string number,
    DateTime issuedAtUtc,
    string payerName,
    string payerSurname,
    Domain.Finance.Payments.PaymentType paymentType,
    decimal amount,
    Domain.Finance.Bills.BillKind kind = Domain.Finance.Bills.BillKind.Regular,
    Guid? originalBillId = null)
    => new()
    {
      Id = id,
      Number = number,
      ReservationId = Guid.NewGuid(),
      IssuedAtUtc = issuedAtUtc,
      CheckInAt = new DateOnly(2026, 5, 1),
      CheckOutAt = new DateOnly(2026, 5, 2),
      LanguageIdGuid = Guid.NewGuid(),
      FinancialClosingId = financialClosingId,
      Kind = kind,
      OriginalBillId = originalBillId,
      Payer = new Domain.Finance.Payers.Payer
      {
        Name = payerName,
        Surname = payerSurname,
        Address = new Domain.Common.Address(Guid.NewGuid(), "Prague", "10000", "Main", "1"),
      },
      LegalEntity = new Domain.Finance.LegalEntities.LegalEntity
      {
        Name = "Acme",
        Address = new Domain.Common.Address(Guid.NewGuid(), "Prague", "10000", "Main", "1"),
        Cin = "12345678",
        Tin = "CZ12345678",
      },
      Payment = new Domain.Finance.Payments.Payment(paymentType, amount),
    };
}
