using Application.Abstractions.Authentication;
using Application.Abstractions.Documents;
using Application.Configuration;
using Application.Finance.FinancialClosings;
using Domain.Common;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.FinancialClosings;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Services.Services;
using Domain.Services.ServiceTypes;
using Infrastructure.Documents.FinancialClosings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using RazorLight;
using SharedKernel;

namespace Infrastructure.UnitTests.Documents.FinancialClosings;

public sealed class RazorLightFinancialClosingReportRendererTests : HandlerTestBase
{
  private readonly IPdfRenderer _pdfRenderer = Substitute.For<IPdfRenderer>();
  private readonly IIdentityService _identity = Substitute.For<IIdentityService>();

  private static readonly CampSettings DefaultCamp = new()
  {
    CheckOutTime = new TimeOnly(11, 0),
    Name = "ATC Olšovec",
    Street = "Kopeček",
    City = "Jedovnice",
    ZipCode = "679 06",
  };

  private RazorLightFinancialClosingReportRenderer CreateSut(CampSettings? camp = null)
  {
    RazorLightEngine engine = new RazorLightEngineBuilder()
      .UseEmbeddedResourcesProject(typeof(FinancialClosingReportModel).Assembly, "Infrastructure.Documents")
      .UseMemoryCachingProvider()
      .Build();

    _identity
      .GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success(new UserDetail(
        Guid.NewGuid(),
        Username: "r61",
        Name: "R61UZ",
        Roles: Array.Empty<string>(),
        IsDisabled: false,
        CreatedAtUtc: DateTime.UtcNow,
        PasskeyCount: 0)));

    return new RazorLightFinancialClosingReportRenderer(
      Db,
      _identity,
      Options.Create(camp ?? DefaultCamp),
      _pdfRenderer,
      engine,
      NullLogger<RazorLightFinancialClosingReportRenderer>.Instance);
  }

  [Fact]
  public async Task RenderAsync_RendersSiteHeaderAndCashier()
  {
    FinancialClosing closing = NewClosing(sequential: 4281, createdByUserId: Guid.NewGuid());

    string capturedHtml = await CaptureHtml(closing);

    capturedHtml.ShouldContain("ATC Olšovec");
    capturedHtml.ShouldContain("Kopeček");
    capturedHtml.ShouldContain("679 06 Jedovnice");
    capturedHtml.ShouldContain("Uzávěrka číslo: 4281");
    capturedHtml.ShouldContain("Recepce R61UZ");
  }

  [Fact]
  public async Task RenderAsync_GroupsBillsByPaymentMethod()
  {
    FinancialClosing closing = NewClosing(sequential: 1);

    var typeId = Guid.NewGuid();
    var svcId = Guid.NewGuid();
    Db.ServiceTypes.Add(new ServiceType { Id = typeId, Name = "Ubytování", IsActive = true });
    Db.Services.Add(new Service { Id = svcId, ServiceGroup = ServiceGroup.Spots, ServiceTypeId = typeId, VatRateId = Guid.NewGuid(), Name = "Spot", IsActive = true });

    var cardBillId = Guid.NewGuid();
    var cashBillId = Guid.NewGuid();
    Db.Bills.Add(NewBill(cardBillId, "K-1", closing.Id, PaymentType.Card, amount: 100m));
    Db.Bills.Add(NewBill(cashBillId, "H-1", closing.Id, PaymentType.Cash, amount: 200m));

    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = cardBillId, ServiceId = svcId, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 100m, VatRatePercentage = 21m });
    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = cashBillId, ServiceId = svcId, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 200m, VatRatePercentage = 21m });

    await Db.SaveChangesAsync();

    string html = await CaptureHtml(closing);

    html.ShouldContain("Platby kartou");
    html.ShouldContain("Platby hotově");
    // Card section appears before Cash section
    html.IndexOf("Platby kartou", StringComparison.Ordinal)
      .ShouldBeLessThan(html.IndexOf("Platby hotově", StringComparison.Ordinal));
  }

  [Fact]
  public async Task RenderAsync_OmitsSectionWithNoBills()
  {
    FinancialClosing closing = NewClosing(sequential: 2);

    var typeId = Guid.NewGuid();
    var svcId = Guid.NewGuid();
    Db.ServiceTypes.Add(new ServiceType { Id = typeId, Name = "Ubytování", IsActive = true });
    Db.Services.Add(new Service { Id = svcId, ServiceGroup = ServiceGroup.Spots, ServiceTypeId = typeId, VatRateId = Guid.NewGuid(), Name = "Spot", IsActive = true });

    var cardBillId = Guid.NewGuid();
    Db.Bills.Add(NewBill(cardBillId, "K-1", closing.Id, PaymentType.Card, amount: 100m));
    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = cardBillId, ServiceId = svcId, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 100m, VatRatePercentage = 21m });

    await Db.SaveChangesAsync();

    string html = await CaptureHtml(closing);

    html.ShouldContain("Platby kartou");
    html.ShouldNotContain("Platby hotově");
  }

  [Fact]
  public async Task RenderAsync_StornoBillsAreMarkedAsChecked()
  {
    FinancialClosing closing = NewClosing(sequential: 3);

    var typeId = Guid.NewGuid();
    var svcId = Guid.NewGuid();
    Db.ServiceTypes.Add(new ServiceType { Id = typeId, Name = "Ubytování", IsActive = true });
    Db.Services.Add(new Service { Id = svcId, ServiceGroup = ServiceGroup.Spots, ServiceTypeId = typeId, VatRateId = Guid.NewGuid(), Name = "Spot", IsActive = true });

    var regularId = Guid.NewGuid();
    var repairId = Guid.NewGuid();
    Db.Bills.Add(NewBill(regularId, "K-100", closing.Id, PaymentType.Card, amount: 50m));
    Bill repair = NewBill(repairId, "K-100R", closing.Id, PaymentType.Card, amount: 50m);
    repair.Kind = BillKind.Repair;
    repair.OriginalBillId = regularId;
    Db.Bills.Add(repair);

    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = regularId, ServiceId = svcId, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 50m, VatRatePercentage = 21m });
    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = repairId, ServiceId = svcId, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 50m, VatRatePercentage = 21m });

    await Db.SaveChangesAsync();

    string html = await CaptureHtml(closing);

    // The regular bill's storno marker has no "checked" class; the repair bill's does.
    // The exact HTML structure (a span beside the bill number) is template-internal, so
    // we assert on substrings that bracket the storno span.
    html.ShouldContain("K-100");
    html.ShouldContain("K-100R");
    html.ShouldContain("storno checked");
    System.Text.RegularExpressions.Regex.Count(html, "storno checked").ShouldBe(1);
  }

  [Fact]
  public async Task RenderAsync_RecreationFeeIsExcludedFromVatColumns()
  {
    FinancialClosing closing = NewClosing(sequential: 4);

    var ubytTypeId = Guid.NewGuid();
    var recTypeId = Guid.NewGuid();
    var spotSvcId = Guid.NewGuid();
    var recSvcId = Guid.NewGuid();

    Db.ServiceTypes.Add(new ServiceType { Id = ubytTypeId, Name = "Ubytování", IsActive = true });
    Db.ServiceTypes.Add(new ServiceType { Id = recTypeId, Name = "Rekreační poplatek", IsActive = true });
    Db.Services.Add(new Service { Id = spotSvcId, ServiceGroup = ServiceGroup.Spots, ServiceTypeId = ubytTypeId, VatRateId = Guid.NewGuid(), Name = "Spot", IsActive = true });
    Db.Services.Add(new Service { Id = recSvcId, ServiceGroup = ServiceGroup.RecreationFees, ServiceTypeId = recTypeId, VatRateId = Guid.NewGuid(), Name = "Recreation fee", IsActive = true });

    var billId = Guid.NewGuid();
    Db.Bills.Add(NewBill(billId, "K210718", closing.Id, PaymentType.Card, amount: 720m));

    // Accommodation 12%: 670 gross → net ≈ 598.21, vat ≈ 71.79 (rounded; the exact numbers
    // depend on rounding direction - we assert footer identities rather than exact cells).
    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = billId, ServiceId = spotSvcId, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 670m, VatRatePercentage = 12m });
    // Recreation fee 50 - excluded from VAT
    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = billId, ServiceId = recSvcId, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 50m, VatRatePercentage = 0m });

    await Db.SaveChangesAsync();

    string html = await CaptureHtml(closing);

    // Service-type columns present for both
    html.ShouldContain("Ubytování");
    html.ShouldContain("Rekreační poplatek");

    // Recreation-fee gross (50) and accommodation gross (670) both surface in the
    // service-type columns; grand total includes the fee.
    html.ShouldContain("50,00");
    html.ShouldContain("670,00");
    html.ShouldContain("720,00");

    // The 12% VAT base is backed out from 670 only - 670 / 1.12 → 598,21. Were the
    // recreation fee (50) accidentally routed into the VAT recap, TotalNet would
    // become 598.21 + 50 = 648,21. Asserting both pins the `continue` guard in
    // BuildBillRow / BuildFooter.
    html.ShouldContain("598,21");
    html.ShouldNotContain("648,21");
    // 12% VAT amount = 670 - 598.21 = 71,79.
    html.ShouldContain("71,79");
  }

  [Fact]
  public async Task RenderAsync_DynamicColumns_RenderEveryDistinctServiceTypeAndVatRate()
  {
    FinancialClosing closing = NewClosing(sequential: 5);

    var t1 = Guid.NewGuid();
    var t2 = Guid.NewGuid();
    var t3 = Guid.NewGuid();
    var s1 = Guid.NewGuid();
    var s2 = Guid.NewGuid();
    var s3 = Guid.NewGuid();
    Db.ServiceTypes.Add(new ServiceType { Id = t1, Name = "Ubytování", IsActive = true });
    Db.ServiceTypes.Add(new ServiceType { Id = t2, Name = "Stravování", IsActive = true });
    Db.ServiceTypes.Add(new ServiceType { Id = t3, Name = "Volný čas", IsActive = true });
    Db.Services.Add(new Service { Id = s1, ServiceGroup = ServiceGroup.Spots, ServiceTypeId = t1, VatRateId = Guid.NewGuid(), Name = "Spot", IsActive = true });
    Db.Services.Add(new Service { Id = s2, ServiceGroup = ServiceGroup.Meals, ServiceTypeId = t2, VatRateId = Guid.NewGuid(), Name = "Meal", IsActive = true });
    Db.Services.Add(new Service { Id = s3, ServiceGroup = ServiceGroup.Others, ServiceTypeId = t3, VatRateId = Guid.NewGuid(), Name = "Activity", IsActive = true });

    var b = Guid.NewGuid();
    Db.Bills.Add(NewBill(b, "K-1", closing.Id, PaymentType.Card, amount: 600m));

    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = b, ServiceId = s1, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 100m, VatRatePercentage = 21m });
    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = b, ServiceId = s2, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 200m, VatRatePercentage = 12m });
    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = b, ServiceId = s3, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 300m, VatRatePercentage = 0m });

    await Db.SaveChangesAsync();

    string html = await CaptureHtml(closing);

    html.ShouldContain("Ubytování");
    html.ShouldContain("Stravování");
    html.ShouldContain("Volný čas");
    html.ShouldContain("Základ (21%)");
    html.ShouldContain("Základ (12%)");
    html.ShouldContain("DPH (21%)");
    html.ShouldContain("DPH (12%)");
    html.ShouldContain("Osvob. od DPH");
  }

  [Fact]
  public async Task RenderAsync_UsesLandscapeA4PageOptions()
  {
    FinancialClosing closing = NewClosing(sequential: 6);

    PdfPageOptions? captured = null;
    _pdfRenderer
      .RenderAsync(Arg.Any<string>(), Arg.Do<PdfPageOptions>(o => captured = o), Arg.Any<CancellationToken>())
      .Returns([0xAA]);

    await CreateSut().RenderAsync(closing, CancellationToken.None);

    captured.ShouldNotBeNull();
    captured!.Landscape.ShouldBeTrue();
    captured.Format.ShouldBe("A4");
  }

  [Fact]
  public async Task RenderAsync_SilentlyDropsItems_WithoutServiceId()
  {
    FinancialClosing closing = NewClosing(sequential: 7);

    var typeId = Guid.NewGuid();
    var svcId = Guid.NewGuid();
    Db.ServiceTypes.Add(new ServiceType { Id = typeId, Name = "Ubytování", IsActive = true });
    Db.Services.Add(new Service { Id = svcId, ServiceGroup = ServiceGroup.Spots, ServiceTypeId = typeId, VatRateId = Guid.NewGuid(), Name = "Spot", IsActive = true });

    var billId = Guid.NewGuid();
    Db.Bills.Add(NewBill(billId, "B-NULL", closing.Id, PaymentType.Card, amount: 100m));

    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = billId, ServiceId = svcId, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 100m, VatRatePercentage = 10m });
    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = billId, ServiceId = null, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 999m, VatRatePercentage = 21m });

    await Db.SaveChangesAsync();

    string html = await CaptureHtml(closing);

    html.ShouldContain("100,00");
    html.ShouldNotContain("999,00");
  }

  [Fact]
  public async Task RenderAsync_OmitsRemovedRowsAndFooterLabels()
  {
    FinancialClosing closing = NewClosing(sequential: 100);

    var typeId = Guid.NewGuid();
    var svcId = Guid.NewGuid();
    Db.ServiceTypes.Add(new ServiceType { Id = typeId, Name = "Ubytování", IsActive = true });
    Db.Services.Add(new Service { Id = svcId, ServiceGroup = ServiceGroup.Spots, ServiceTypeId = typeId, VatRateId = Guid.NewGuid(), Name = "Spot", IsActive = true });

    var billId = Guid.NewGuid();
    Db.Bills.Add(NewBill(billId, "K-1", closing.Id, PaymentType.Card, amount: 100m));
    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = billId, ServiceId = svcId, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 100m, VatRatePercentage = 21m });

    await Db.SaveChangesAsync();

    string html = await CaptureHtml(closing);

    html.ShouldNotContain("Odpočet zdaněných záloh celkem");
    html.ShouldNotContain("Celkem (včetně záloh)");
    html.ShouldNotContain("Zd. plnění:");
    html.ShouldNotContain("Osvobozeno:");
    html.ShouldNotContain("Poplatek OU:");
  }

  private async Task<string> CaptureHtml(FinancialClosing closing)
  {
    string? capturedHtml = null;
    _pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<PdfPageOptions>(), Arg.Any<CancellationToken>())
      .Returns(new byte[] { 0xAA });

    Result<byte[]> result = await CreateSut().RenderAsync(closing, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    capturedHtml.ShouldNotBeNull();
    return System.Net.WebUtility.HtmlDecode(capturedHtml!);
  }

  private static FinancialClosing NewClosing(uint sequential, Guid? createdByUserId = null) => new()
  {
    Id = Guid.NewGuid(),
    FinancialClosingId = sequential,
    ClosedAtUtc = new DateTime(2026, 5, 12, 17, 54, 0, DateTimeKind.Utc),
    TotalAmount = 0m,
    CreatedByUserId = createdByUserId,
  };

  private static Address MinAddress() => new(
    Guid.NewGuid(),
    "Prague",
    "10000",
    "Main St",
    "1");

  [Fact]
  public async Task RenderAsync_IncludesAllActiveServiceTypes_EvenWithoutItemsInClosing()
  {
    FinancialClosing closing = NewClosing(sequential: 200);

    var usedTypeId = Guid.NewGuid();
    var unusedTypeId = Guid.NewGuid();
    var svcId = Guid.NewGuid();
    Db.ServiceTypes.Add(new ServiceType { Id = usedTypeId, Name = "Ubytování", IsActive = true });
    Db.ServiceTypes.Add(new ServiceType { Id = unusedTypeId, Name = "Stravování", IsActive = true });
    Db.Services.Add(new Service { Id = svcId, ServiceGroup = ServiceGroup.Spots, ServiceTypeId = usedTypeId, VatRateId = Guid.NewGuid(), Name = "Spot", IsActive = true });

    var billId = Guid.NewGuid();
    Db.Bills.Add(NewBill(billId, "K-1", closing.Id, PaymentType.Card, amount: 100m));
    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = billId, ServiceId = svcId, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 100m, VatRatePercentage = 21m });

    await Db.SaveChangesAsync();

    string html = await CaptureHtml(closing);

    html.ShouldContain("Ubytování");
    html.ShouldContain("Stravování");
  }

  [Fact]
  public async Task RenderAsync_IncludesInactiveServiceTypes_WhenReferencedByClosingItems()
  {
    FinancialClosing closing = NewClosing(sequential: 210);

    // The service type is inactive but a bill item in this closing references it,
    // so it must still surface as a column through the items-arm of the union.
    var deactivatedTypeId = Guid.NewGuid();
    var svcId = Guid.NewGuid();
    Db.ServiceTypes.Add(new ServiceType { Id = deactivatedTypeId, Name = "Půjčovna", IsActive = false });
    Db.Services.Add(new Service { Id = svcId, ServiceGroup = ServiceGroup.Others, ServiceTypeId = deactivatedTypeId, VatRateId = Guid.NewGuid(), Name = "Půjčovna kol", IsActive = true });

    var billId = Guid.NewGuid();
    Db.Bills.Add(NewBill(billId, "K-1", closing.Id, PaymentType.Card, amount: 100m));
    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = billId, ServiceId = svcId, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 100m, VatRatePercentage = 21m });

    await Db.SaveChangesAsync();

    string html = await CaptureHtml(closing);

    html.ShouldContain("Půjčovna");
  }

  [Fact]
  public async Task RenderAsync_IncludesAllActiveVatRates_EvenWithoutItemsInClosing()
  {
    FinancialClosing closing = NewClosing(sequential: 300);

    // The 12% active VatRate has no matching items in this closing; it must surface
    // as a column purely through the active-VatRates arm of the union.
    Db.VatRates.Add(new Domain.Services.VatRates.VatRate { Id = Guid.NewGuid(), Name = "Reduced", Rate = 12m, IsActive = true });

    var typeId = Guid.NewGuid();
    var svcId = Guid.NewGuid();
    Db.ServiceTypes.Add(new ServiceType { Id = typeId, Name = "Ubytování", IsActive = true });
    Db.Services.Add(new Service { Id = svcId, ServiceGroup = ServiceGroup.Spots, ServiceTypeId = typeId, VatRateId = Guid.NewGuid(), Name = "Spot", IsActive = true });

    // Item at 21% - only there so rendering has data; 21% is covered by the items-arm.
    var billId = Guid.NewGuid();
    Db.Bills.Add(NewBill(billId, "K-1", closing.Id, PaymentType.Card, amount: 100m));
    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = billId, ServiceId = svcId, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 100m, VatRatePercentage = 21m });

    await Db.SaveChangesAsync();

    string html = await CaptureHtml(closing);

    // 12% headers come purely from the active-VatRates arm.
    html.ShouldContain("Základ (12%)");
    html.ShouldContain("DPH (12%)");
    // 21% headers come from the items-arm.
    html.ShouldContain("Základ (21%)");
    html.ShouldContain("DPH (21%)");
  }

  [Fact]
  public async Task RenderAsync_GrossUsesRecapSingleTimesRecapDayTimesUnitPrice()
  {
    FinancialClosing closing = NewClosing(sequential: 500);

    var typeId = Guid.NewGuid();
    var svcId = Guid.NewGuid();
    Db.ServiceTypes.Add(new ServiceType { Id = typeId, Name = "Ubytování", IsActive = true });
    Db.Services.Add(new Service { Id = svcId, ServiceGroup = ServiceGroup.Spots, ServiceTypeId = typeId, VatRateId = Guid.NewGuid(), Name = "Spot", IsActive = true });

    var billId = Guid.NewGuid();
    Db.Bills.Add(NewBill(billId, "K-1", closing.Id, PaymentType.Card, amount: 600m));

    // recapSingle (2) × recapDay (3) × unitPrice (100) = 600 gross.
    // `Quantity` is a frontend display field; the legacy formula Quantity*UnitPrice
    // would yield 100 - assert against 600 to pin the recap-based formula.
    Db.BillItems.Add(new BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = svcId,
      Quantity = 1,
      UnitPrice = 100m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 2,
      RecapDayQuantity = 3,
    });

    await Db.SaveChangesAsync();

    string html = await CaptureHtml(closing);

    html.ShouldContain("600,00");
    html.ShouldNotContain("100,00");
  }

  [Fact]
  public async Task RenderAsync_IncludesVatRatesUsedOnAnyBill_EvenWhenInactive()
  {
    FinancialClosing closing = NewClosing(sequential: 400);

    var typeId = Guid.NewGuid();
    var svcId = Guid.NewGuid();
    Db.ServiceTypes.Add(new ServiceType { Id = typeId, Name = "Ubytování", IsActive = true });
    Db.Services.Add(new Service { Id = svcId, ServiceGroup = ServiceGroup.Spots, ServiceTypeId = typeId, VatRateId = Guid.NewGuid(), Name = "Spot", IsActive = true });

    // Bill IN this closing, at 21%
    var billInClosingId = Guid.NewGuid();
    Db.Bills.Add(NewBill(billInClosingId, "K-1", closing.Id, PaymentType.Card, amount: 100m));
    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = billInClosingId, ServiceId = svcId, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 100m, VatRatePercentage = 21m });

    // Bill NOT in this closing, at a historical 10% rate (and no VatRate row at all)
    var orphanBillId = Guid.NewGuid();
    Bill orphanBill = NewBill(orphanBillId, "K-OLD", Guid.NewGuid(), PaymentType.Card, amount: 50m);
    orphanBill.FinancialClosingId = null;
    Db.Bills.Add(orphanBill);
    Db.BillItems.Add(new BillItem { Id = Guid.NewGuid(), BillId = orphanBillId, ServiceId = svcId, Quantity = 1, RecapSingleQuantity = 1, RecapDayQuantity = 1, UnitPrice = 50m, VatRatePercentage = 10m });

    await Db.SaveChangesAsync();

    string html = await CaptureHtml(closing);

    html.ShouldContain("Základ (21%)");
    html.ShouldContain("Základ (10%)");
    html.ShouldContain("DPH (10%)");
  }

  private static Bill NewBill(Guid id, string number, Guid closingId, PaymentType paymentType, decimal amount) =>
    new()
    {
      Id = id,
      Number = number,
      ReservationId = Guid.NewGuid(),
      IssuedAtUtc = new DateTime(2026, 5, 12, 12, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 5, 11),
      CheckOutAt = new DateOnly(2026, 5, 13),
      LanguageIdGuid = Guid.NewGuid(),
      FinancialClosingId = closingId,
      Payer = new Payer
      {
        Name = "John",
        Surname = "Doe",
        Address = MinAddress(),
      },
      LegalEntity = new LegalEntity
      {
        Name = "Acme s.r.o.",
        Address = MinAddress(),
        Cin = "12345678",
        Tin = "CZ12345678",
      },
      Payment = new Payment(paymentType, amount),
    };
}
