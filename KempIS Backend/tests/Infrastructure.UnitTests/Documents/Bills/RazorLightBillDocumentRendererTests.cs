using Application.Abstractions.Documents;
using Application.Configuration;
using Application.Finance.Bills;
using Domain.Common;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.InvoiceItems;
using Domain.Finance.Invoices;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Services.Languages;
using Infrastructure.Documents.Bills;
using Infrastructure.Documents.Bills.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RazorLight;
using SharedKernel;

namespace Infrastructure.UnitTests.Documents.Bills;

public sealed class RazorLightBillDocumentRendererTests : HandlerTestBase
{
  private static readonly CampSettings DefaultCamp = new()
  {
    CheckOutTime = new TimeOnly(11, 0),
    Name = "ATC Olšovec",
    Street = "Kopeček",
    City = "Jedovnice",
    ZipCode = "679 06",
    Cin = "12345678",
    Tin = "CZ12345678",
    Phone = "+420 516 442 216",
    Email = "info@atcolsovec.cz",
    Web = "https://www.atcolsovec.cz",
  };

  private static Address MinAddress() => new(
    Guid.NewGuid(),
    "Prague",
    "10000",
    "Main St",
    "1");

  private static Bill NewBill(Guid id, Guid languageId) =>
    new()
    {
      Id = id,
      Number = "2024-001",
      ReservationId = Guid.NewGuid(),
      IssuedAtUtc = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 4, 20),
      CheckOutAt = new DateOnly(2026, 4, 22),
      LanguageIdGuid = languageId,
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
      Payment = new Payment(PaymentType.Cash, 100m),
    };

  private static BillItem NewBillItem(Guid billId) =>
    new()
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = null,
      Quantity = 2,
      UnitPrice = 50m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 1,
      RecapDayQuantity = 1,
    };

  private static IStringLocalizer<BillResources> BuildLocalizer()
  {
    ServiceCollection services = new();
    services.AddLogging();
    services.AddLocalization();
    ServiceProvider provider = services.BuildServiceProvider();
    return provider.GetRequiredService<IStringLocalizer<BillResources>>();
  }

  private RazorLightBillDocumentRenderer CreateSut(IPdfRenderer pdfRenderer, CampSettings? camp = null) =>
    new(
      Db,
      pdfRenderer,
      BuildLocalizer(),
      Options.Create(camp ?? DefaultCamp),
      new RazorLightEngineBuilder()
        .UseEmbeddedResourcesProject(typeof(BillResources).Assembly, "Infrastructure.Documents")
        .UseMemoryCachingProvider()
        .Build(),
      NullLogger<RazorLightBillDocumentRenderer>.Instance);

  [Theory]
  [InlineData("en-US", "Tax invoice", "Thank you")]
  [InlineData("cs-CZ", "Daňový doklad", "Děkujeme")]
  public async Task RenderAsync_UsesLocalizedStrings_BasedOnBillLanguage(
    string languageCode,
    string expectedTitle,
    string expectedThanks)
  {
    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();

    Language language = new()
    {
      Id = languageId,
      Code = languageCode,
      Name = languageCode,
    };

    Bill bill = NewBill(billId, languageId);
    BillItem item = NewBillItem(billId);

    Db.Languages.Add(language);
    Db.Bills.Add(bill);
    Db.BillItems.Add(item);
    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(new byte[] { 1, 2, 3 }));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Content.ShouldBe(new byte[] { 1, 2, 3 });
    result.Value.LanguageCode.ShouldBe(languageCode);
    string decodedHtml = System.Net.WebUtility.HtmlDecode(capturedHtml);
    decodedHtml.ShouldContain(expectedTitle);
    decodedHtml.ShouldContain(expectedThanks);
  }

  [Fact]
  public async Task RenderAsync_RegularBill_RendersTaxInvoiceTitle()
  {
    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();

    Db.Languages.Add(new Language { Id = languageId, Code = "cs-CZ", Name = "Czech" });

    Bill bill = new()
    {
      Id = billId,
      Number = "B-001",
      Kind = BillKind.Regular,
      LanguageIdGuid = languageId,
      IssuedAtUtc = new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 5, 17),
      CheckOutAt = new DateOnly(2026, 5, 19),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = MinAddress() },
      Payment = new Payment(PaymentType.Cash, 100m),
    };

    Db.Bills.Add(bill);
    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(new byte[] { 0xAA }));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);
    html.ShouldContain("Daňový doklad");
    html.ShouldNotContain("Opravný daňový doklad");
  }

  [Fact]
  public async Task RenderAsync_RepairBill_RendersCorrectiveTaxInvoiceTitle()
  {
    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();

    Db.Languages.Add(new Language { Id = languageId, Code = "cs-CZ", Name = "Czech" });

    Bill bill = new()
    {
      Id = billId,
      Number = "B-002",
      Kind = BillKind.Repair,
      LanguageIdGuid = languageId,
      IssuedAtUtc = new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 5, 17),
      CheckOutAt = new DateOnly(2026, 5, 19),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = MinAddress() },
      Payment = new Payment(PaymentType.Cash, 100m),
    };

    Db.Bills.Add(bill);
    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(new byte[] { 0xBB }));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);
    html.ShouldContain("Opravný daňový doklad");
  }

  [Fact]
  public async Task RenderAsync_ReturnsNotFound_WhenBillMissing()
  {
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(Guid.NewGuid(), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Bill.NotFound");
    await pdfRenderer.DidNotReceive().RenderAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Renders_BillWithDeduction_ShowsInvoiceVatRecap()
  {
    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();
    var invoiceId = Guid.NewGuid();

    Db.Languages.Add(new Language { Id = languageId, Code = "en-US", Name = "English" });

    Bill bill = new()
    {
      Id = billId,
      Number = "2026-042",
      ReservationId = Guid.NewGuid(),
      IssuedAtUtc = new DateTime(2026, 4, 22, 10, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 4, 20),
      CheckOutAt = new DateOnly(2026, 4, 22),
      LanguageIdGuid = languageId,
      Payer = new Payer { Name = "Jan", Surname = "Novak", Address = MinAddress() },
      LegalEntity = new LegalEntity { Name = "Camp s.r.o.", Address = MinAddress(), Cin = "11111111", Tin = "CZ11111111" },
      Payment = new Payment(PaymentType.Card, 250m),
    };
    Db.Bills.Add(bill);

    Db.BillItems.Add(new BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = null,
      Quantity = 2,
      UnitPrice = 50m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 1,
      RecapDayQuantity = 1,
    });

    Db.BillItems.Add(new BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = null,
      Quantity = 1,
      UnitPrice = 80m,
      VatRatePercentage = 12m,
      RecapSingleQuantity = 1,
      RecapDayQuantity = 1,
    });

    Db.Invoices.Add(new Invoice
    {
      Id = invoiceId,
      ReservationId = Guid.NewGuid(),
      Number = "EXT-8888",
      Status = InvoiceStatus.Paid,
      IssuedAt = new DateOnly(2026, 4, 1),
      PaidAt = new DateOnly(2026, 4, 10),
      LinkedBillId = billId,
      Email = "seed@example.com",
      PhoneNumber = "+420000000000",
      Payer = new Payer { Name = "Jan", Surname = "Novak", Address = MinAddress() },
    });

    // Prepaid service has no ServiceText for this language → renders via Service.Name.
    var prepaidServiceId = Guid.NewGuid();
    Db.Services.Add(new Domain.Services.Services.Service
    {
      Id = prepaidServiceId,
      ServiceGroup = Domain.Services.Services.ServiceGroup.Spots,
      ServiceTypeId = Guid.NewGuid(),
      VatRateId = Guid.NewGuid(),
      Name = "Prepaid service",
      BasePrice = 20m,
      IsActive = true,
    });

    Db.InvoiceItems.Add(new InvoiceItem
    {
      Id = Guid.NewGuid(),
      InvoiceId = invoiceId,
      ServiceGuid = prepaidServiceId,
      Quantity = 3m,
      UnitPrice = 20m,
      VatRatePercentage = 15m,
    });

    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(Array.Empty<byte>()));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);

    html.ShouldContain("EXT-8888");
    html.ShouldContain("Paid by invoice");
    html.ShouldContain("Deducted");
    html.ShouldContain("VAT recap");

    html.ShouldContain("12 %");
    html.ShouldContain("15 %");
    html.ShouldContain("21 %");

    html.ShouldContain("Total due");
  }

  [Fact]
  public async Task Renders_BillWithoutDeductions_SkipsDeductionSection()
  {
    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();

    Db.Languages.Add(new Language { Id = languageId, Code = "en-US", Name = "English" });

    Bill bill = new()
    {
      Id = billId,
      Number = "2026-001",
      ReservationId = Guid.NewGuid(),
      IssuedAtUtc = new DateTime(2026, 4, 22, 10, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 4, 20),
      CheckOutAt = new DateOnly(2026, 4, 22),
      LanguageIdGuid = languageId,
      Payer = new Payer { Name = "Eva", Surname = "Mala", Address = MinAddress() },
      LegalEntity = new LegalEntity { Name = "Camp s.r.o.", Address = MinAddress(), Cin = "22222222", Tin = "CZ22222222" },
      Payment = new Payment(PaymentType.Cash, 121m),
    };
    Db.Bills.Add(bill);

    Db.BillItems.Add(new BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = null,
      Quantity = 1,
      UnitPrice = 100m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 1,
      RecapDayQuantity = 1,
    });

    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(Array.Empty<byte>()));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);

    html.ShouldNotContain("Paid by invoice");
  }

  [Fact]
  public async Task RenderAsync_BillItemWithServiceText_RendersTranslatedDescription()
  {
    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();

    Db.Languages.Add(new Language { Id = languageId, Code = "en-US", Name = "English" });

    // HandlerTestBase disables FK enforcement, so we can skip ServiceType/VatRate rows.
    Domain.Services.Services.Service service = new()
    {
      Id = serviceId,
      ServiceGroup = Domain.Services.Services.ServiceGroup.Spots,
      ServiceTypeId = Guid.NewGuid(),
      VatRateId = Guid.NewGuid(),
      Name = "Pitch A",
      BasePrice = 50m,
      IsActive = true,
    };
    Db.Services.Add(service);

    Db.ServiceTexts.Add(new Domain.Services.ServiceTexts.ServiceText
    {
      Id = Guid.NewGuid(),
      ServiceId = serviceId,
      LanguageId = languageId,
      PrintText = "Camping pitch - deluxe",
    });

    Bill bill = NewBill(billId, languageId);
    Db.Bills.Add(bill);

    BillItem item = NewBillItem(billId);
    item.ServiceId = serviceId;
    Db.BillItems.Add(item);

    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(Array.Empty<byte>()));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);
    html.ShouldContain("Camping pitch - deluxe");
    html.ShouldNotContain("Pitch A");
    html.ShouldNotContain(serviceId.ToString());
  }

  [Fact]
  public async Task RenderAsync_BillItemWithoutMatchingServiceText_FallsBackToServiceName()
  {
    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();
    var otherLanguageId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();

    Db.Languages.Add(new Language { Id = languageId, Code = "en-US", Name = "English" });
    Db.Languages.Add(new Language { Id = otherLanguageId, Code = "cs-CZ", Name = "Czech" });

    Domain.Services.Services.Service service = new()
    {
      Id = serviceId,
      ServiceGroup = Domain.Services.Services.ServiceGroup.Spots,
      ServiceTypeId = Guid.NewGuid(),
      VatRateId = Guid.NewGuid(),
      Name = "Electricity hookup",
      BasePrice = 5m,
      IsActive = true,
    };
    Db.Services.Add(service);

    // ServiceText exists, but for a different language than the bill's.
    Db.ServiceTexts.Add(new Domain.Services.ServiceTexts.ServiceText
    {
      Id = Guid.NewGuid(),
      ServiceId = serviceId,
      LanguageId = otherLanguageId,
      PrintText = "Elektřina",
    });

    Bill bill = NewBill(billId, languageId);
    Db.Bills.Add(bill);

    BillItem item = NewBillItem(billId);
    item.ServiceId = serviceId;
    Db.BillItems.Add(item);

    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(Array.Empty<byte>()));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);
    html.ShouldContain("Electricity hookup");
    html.ShouldNotContain("Elektřina");
  }

  [Fact]
  public async Task RenderAsync_BillItemWithNullServiceId_RendersEmptyDescription()
  {
    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();

    Db.Languages.Add(new Language { Id = languageId, Code = "en-US", Name = "English" });

    Bill bill = NewBill(billId, languageId);
    Db.Bills.Add(bill);

    Db.BillItems.Add(NewBillItem(billId));

    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(Array.Empty<byte>()));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public async Task RenderAsync_OmitsDicSegment_WhenLegalEntityTinIsNull()
  {
    CampSettings campWithoutTin = new()
    {
      CheckOutTime = new TimeOnly(11, 0),
      Name = "ATC Olšovec",
      Street = "Kopeček",
      City = "Jedovnice",
      ZipCode = "679 06",
      Cin = "12345678",
    };

    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();

    Db.Languages.Add(new Language { Id = languageId, Code = "en-US", Name = "en-US" });

    Bill bill = NewBill(billId, languageId);
    bill.LegalEntity!.Tin = null;

    Db.Bills.Add(bill);
    Db.BillItems.Add(NewBillItem(billId));
    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(new byte[] { 1, 2, 3 }));

    await CreateSut(pdfRenderer, campWithoutTin).RenderAsync(billId, CancellationToken.None);

    capturedHtml.ShouldContain("CIN:");
    capturedHtml.ShouldNotContain("VAT ID:");
  }

  [Fact]
  public async Task RenderAsync_RendersCampLegalBlock_WhenAllFieldsPresent()
  {
    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();

    Db.Languages.Add(new Language { Id = languageId, Code = "cs-CZ", Name = "Czech" });

    Bill bill = new()
    {
      Id = billId,
      Number = "B-CAMP-1",
      Kind = BillKind.Regular,
      LanguageIdGuid = languageId,
      IssuedAtUtc = new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 5, 17),
      CheckOutAt = new DateOnly(2026, 5, 19),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = MinAddress() },
      Payment = new Payment(PaymentType.Cash, 100m),
    };
    Db.Bills.Add(bill);
    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(new byte[] { 0xAA }));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);
    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);

    html.ShouldContain("ATC Olšovec");
    html.ShouldContain("Kopeček");
    html.ShouldContain("679 06 Jedovnice");
    html.ShouldContain("IČO: 12345678");
    html.ShouldContain("DIČ: CZ12345678");
    html.ShouldContain("+420 516 442 216");
    html.ShouldContain("info@atcolsovec.cz");
    html.ShouldContain("https://www.atcolsovec.cz");
  }

  [Fact]
  public async Task RenderAsync_OmitsCampLegalFields_WhenEmpty()
  {
    CampSettings camp = new()
    {
      CheckOutTime = new TimeOnly(11, 0),
      Name = "Camp Name",
      Street = "Some street",
      City = "Some city",
      ZipCode = "12345",
      // Cin/Tin/Phone/Email/Web left empty for this test
    };

    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();

    Db.Languages.Add(new Language { Id = languageId, Code = "cs-CZ", Name = "Czech" });

    Bill bill = new()
    {
      Id = billId,
      Number = "B-CAMP-2",
      Kind = BillKind.Regular,
      LanguageIdGuid = languageId,
      IssuedAtUtc = new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 5, 17),
      CheckOutAt = new DateOnly(2026, 5, 19),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = MinAddress() },
      Payment = new Payment(PaymentType.Cash, 100m),
    };
    Db.Bills.Add(bill);
    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(new byte[] { 0xAA }));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer, camp).RenderAsync(billId, CancellationToken.None);
    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);

    html.ShouldNotContain("IČO:");
    html.ShouldNotContain("DIČ:");
    html.ShouldNotContain("tel:");
  }

  [Fact]
  public async Task RenderAsync_RendersTaxableSupplyDateLabel()
  {
    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();

    Db.Languages.Add(new Language { Id = languageId, Code = "cs-CZ", Name = "Czech" });

    Bill bill = new()
    {
      Id = billId,
      Number = "B-TAX-1",
      Kind = BillKind.Regular,
      LanguageIdGuid = languageId,
      IssuedAtUtc = new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 5, 17),
      CheckOutAt = new DateOnly(2026, 5, 19),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = MinAddress() },
      Payment = new Payment(PaymentType.Cash, 100m),
    };
    Db.Bills.Add(bill);
    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(new byte[] { 0xAA }));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);
    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);

    html.ShouldContain("Datum vystavení");
    html.ShouldContain("Datum zdanitelného plnění");
  }

  [Fact]
  public async Task RenderAsync_ItemRow_SuffixesVehicleRegistrationsForVehicleService()
  {
    var serviceId = Guid.NewGuid();
    Db.Services.Add(new Domain.Services.Services.Service
    {
      Id = serviceId,
      ServiceGroup = Domain.Services.Services.ServiceGroup.Vehicles,
      ServiceTypeId = Guid.NewGuid(),
      VatRateId = Guid.NewGuid(),
      Name = "Parkování",
      IsActive = true,
    });

    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();
    Db.Languages.Add(new Language { Id = languageId, Code = "cs-CZ", Name = "Czech" });

    Bill bill = new()
    {
      Id = billId,
      Number = "B-VEH-1",
      Kind = BillKind.Regular,
      LanguageIdGuid = languageId,
      IssuedAtUtc = new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 5, 17),
      CheckOutAt = new DateOnly(2026, 5, 19),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = MinAddress() },
      Payment = new Payment(PaymentType.Cash, 100m),
    };
    Db.Bills.Add(bill);

    Db.BillItems.Add(new BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = serviceId,
      Quantity = 1,
      UnitPrice = 100m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 1,
      RecapDayQuantity = 1,
    });
    Db.Vehicles.Add(new Domain.Reservations.Vehicles.Vehicle
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = serviceId,
      RegistrationNumber = "4P12345",
    });
    Db.Vehicles.Add(new Domain.Reservations.Vehicles.Vehicle
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = serviceId,
      RegistrationNumber = "6P67890",
    });
    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(new byte[] { 0xAA }));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);
    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);

    html.ShouldContain("Parkování (4P12345, 6P67890)");
  }

  [Fact]
  public async Task RenderAsync_ItemRow_SuffixesSpotNamesForSpotService()
  {
    var serviceId = Guid.NewGuid();
    var spotGroupId = Guid.NewGuid();
    var spotA = Guid.NewGuid();
    var spotB = Guid.NewGuid();

    Db.Services.Add(new Domain.Services.Services.Service
    {
      Id = serviceId,
      ServiceGroup = Domain.Services.Services.ServiceGroup.Spots,
      ServiceTypeId = Guid.NewGuid(),
      VatRateId = Guid.NewGuid(),
      Name = "Místo",
      IsActive = true,
    });
    Db.SpotGroups.Add(new Domain.Reservations.SpotGroups.SpotGroup
    {
      Id = spotGroupId,
      ServiceId = serviceId,
      Name = "Hlavní pole",
      Capacity = 10,
      IsActive = true,
      ImageUrl = "",
      DetailsUrl = "",
    });
    Db.Spots.Add(new Domain.Reservations.Spots.Spot { Id = spotA, SpotGroupId = spotGroupId, Name = "A1", IsActive = true });
    Db.Spots.Add(new Domain.Reservations.Spots.Spot { Id = spotB, SpotGroupId = spotGroupId, Name = "A2", IsActive = true });

    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();
    Db.Languages.Add(new Language { Id = languageId, Code = "cs-CZ", Name = "Czech" });

    Bill bill = new()
    {
      Id = billId,
      Number = "B-SPOT-1",
      Kind = BillKind.Regular,
      LanguageIdGuid = languageId,
      IssuedAtUtc = new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 5, 17),
      CheckOutAt = new DateOnly(2026, 5, 19),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = MinAddress() },
      Payment = new Payment(PaymentType.Cash, 100m),
    };
    Db.Bills.Add(bill);

    Db.BillItems.Add(new BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = serviceId,
      Quantity = 1,
      UnitPrice = 100m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 1,
      RecapDayQuantity = 1,
    });

    // ReservationId doesn't need to match the bill's reservation - renderer joins on BillId.
    // FK enforcement is disabled in HandlerTestBase.
    Db.ReservationSpotItems.Add(new Domain.Reservations.ReservationSpotItems.ReservationSpotItem
    {
      Id = Guid.NewGuid(),
      ReservationId = Guid.NewGuid(),
      SpotGroupId = spotGroupId,
      SpotId = spotA,
      BillId = billId,
    });
    Db.ReservationSpotItems.Add(new Domain.Reservations.ReservationSpotItems.ReservationSpotItem
    {
      Id = Guid.NewGuid(),
      ReservationId = Guid.NewGuid(),
      SpotGroupId = spotGroupId,
      SpotId = spotB,
      BillId = billId,
    });
    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(new byte[] { 0xAA }));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);
    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);

    html.ShouldContain("Místo (A1, A2)");
  }

  [Fact]
  public async Task RenderAsync_VatRecap_UnionsActiveAndUsedRates()
  {
    // Active rates: 12 (unused on this bill), 21 (used by bill item)
    // Invoice item rate: 10 (not in active set - historical)
    Db.VatRates.Add(new Domain.Services.VatRates.VatRate { Id = Guid.NewGuid(), Name = "Reduced", Rate = 12m, IsActive = true });
    Db.VatRates.Add(new Domain.Services.VatRates.VatRate { Id = Guid.NewGuid(), Name = "Standard", Rate = 21m, IsActive = true });

    var serviceId = Guid.NewGuid();
    Db.Services.Add(new Domain.Services.Services.Service
    {
      Id = serviceId,
      ServiceGroup = Domain.Services.Services.ServiceGroup.Spots,
      ServiceTypeId = Guid.NewGuid(),
      VatRateId = Guid.NewGuid(),
      Name = "Místo",
      IsActive = true,
    });

    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();
    Db.Languages.Add(new Language { Id = languageId, Code = "cs-CZ", Name = "Czech" });

    Bill bill = new()
    {
      Id = billId,
      Number = "B-RECAP-1",
      Kind = BillKind.Regular,
      LanguageIdGuid = languageId,
      IssuedAtUtc = new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 5, 17),
      CheckOutAt = new DateOnly(2026, 5, 19),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = MinAddress() },
      Payment = new Payment(PaymentType.Cash, 121m),
    };
    Db.Bills.Add(bill);

    Db.BillItems.Add(new BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = serviceId,
      Quantity = 1,
      UnitPrice = 121m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 1,
      RecapDayQuantity = 1,
    });

    // Linked invoice with a historical 10% rate
    var invoiceId = Guid.NewGuid();
    Db.Invoices.Add(new Domain.Finance.Invoices.Invoice
    {
      Id = invoiceId,
      ReservationId = Guid.NewGuid(),
      Number = "F-001",
      Status = Domain.Finance.Invoices.InvoiceStatus.Paid,
      IssuedAt = new DateOnly(2026, 5, 1),
      Email = "",
      PhoneNumber = "",
      LinkedBillId = billId,
    });
    Db.InvoiceItems.Add(new Domain.Finance.InvoiceItems.InvoiceItem
    {
      Id = Guid.NewGuid(),
      InvoiceId = invoiceId,
      ServiceGuid = serviceId,
      Quantity = 1,
      UnitPrice = 110m,
      VatRatePercentage = 10m,
    });

    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(new byte[] { 0xAA }));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);
    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);

    // All three rates appear as rows in the main bill VAT recap, each in its own rate cell
    html.ShouldContain(">10 %<");
    html.ShouldContain(">12 %<");
    html.ShouldContain(">21 %<");
  }

  [Fact]
  public async Task RenderAsync_ItemRow_RendersAllSevenColumns()
  {
    var serviceId = Guid.NewGuid();
    Db.Services.Add(new Domain.Services.Services.Service
    {
      Id = serviceId,
      ServiceGroup = Domain.Services.Services.ServiceGroup.Spots,
      ServiceTypeId = Guid.NewGuid(),
      VatRateId = Guid.NewGuid(),
      Name = "Místo",
      IsActive = true,
    });

    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();
    Db.Languages.Add(new Language { Id = languageId, Code = "cs-CZ", Name = "Czech" });

    Bill bill = new()
    {
      Id = billId,
      Number = "B-ITEM-7",
      Kind = BillKind.Regular,
      LanguageIdGuid = languageId,
      IssuedAtUtc = new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 5, 17),
      CheckOutAt = new DateOnly(2026, 5, 19),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = MinAddress() },
      Payment = new Payment(PaymentType.Cash, 726m),
    };
    Db.Bills.Add(bill);

    Db.BillItems.Add(new BillItem
    {
      Id = Guid.NewGuid(),
      BillId = billId,
      ServiceId = serviceId,
      Quantity = 1,
      UnitPrice = 121m,
      VatRatePercentage = 21m,
      RecapSingleQuantity = 2,
      RecapDayQuantity = 3,
    });
    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(new byte[] { 0xAA }));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);
    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);

    // Headers
    html.ShouldContain("Popis");
    html.ShouldContain("Cena za jedn.");
    html.ShouldContain("Cena s DPH");
    html.ShouldContain("J");
    html.ShouldContain("D");
    html.ShouldContain("Sazba");
    html.ShouldContain("Celk. cena");

    // Row values
    html.ShouldContain("Místo");
    html.ShouldContain("100,00");    // net unit price = 121 / 1.21 rounded
    html.ShouldContain("121,00");    // gross unit price
    html.ShouldContain(">2<");       // RecapSingleQuantity column body
    html.ShouldContain(">3<");       // RecapDayQuantity column body
    html.ShouldContain(">21<");      // VAT rate column body
    html.ShouldContain("726,00");    // line total = 2 × 3 × 121
  }

  [Fact]
  public async Task RenderAsync_LinkedInvoice_RendersOnlyUsedVatRates()
  {
    // Active rates include 15% - unused on this invoice and should NOT appear in the per-invoice recap.
    Db.VatRates.Add(new Domain.Services.VatRates.VatRate { Id = Guid.NewGuid(), Name = "Other", Rate = 15m, IsActive = true });
    Db.VatRates.Add(new Domain.Services.VatRates.VatRate { Id = Guid.NewGuid(), Name = "Reduced", Rate = 12m, IsActive = true });
    Db.VatRates.Add(new Domain.Services.VatRates.VatRate { Id = Guid.NewGuid(), Name = "Standard", Rate = 21m, IsActive = true });

    var serviceId = Guid.NewGuid();
    Db.Services.Add(new Domain.Services.Services.Service
    {
      Id = serviceId,
      ServiceGroup = Domain.Services.Services.ServiceGroup.Spots,
      ServiceTypeId = Guid.NewGuid(),
      VatRateId = Guid.NewGuid(),
      Name = "Místo",
      IsActive = true,
    });

    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();
    Db.Languages.Add(new Language { Id = languageId, Code = "cs-CZ", Name = "Czech" });

    Bill bill = new()
    {
      Id = billId,
      Number = "B-DED-1",
      Kind = BillKind.Regular,
      LanguageIdGuid = languageId,
      IssuedAtUtc = new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 5, 17),
      CheckOutAt = new DateOnly(2026, 5, 19),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = MinAddress() },
      Payment = new Payment(PaymentType.Cash, 0m),
    };
    Db.Bills.Add(bill);

    var invoiceId = Guid.NewGuid();
    Db.Invoices.Add(new Domain.Finance.Invoices.Invoice
    {
      Id = invoiceId,
      ReservationId = Guid.NewGuid(),
      Number = "F-INV-1",
      Status = Domain.Finance.Invoices.InvoiceStatus.Paid,
      IssuedAt = new DateOnly(2026, 5, 1),
      Email = "",
      PhoneNumber = "",
      LinkedBillId = billId,
    });
    Db.InvoiceItems.Add(new Domain.Finance.InvoiceItems.InvoiceItem
    {
      Id = Guid.NewGuid(),
      InvoiceId = invoiceId,
      ServiceGuid = serviceId,
      Quantity = 1,
      UnitPrice = 121m,
      VatRatePercentage = 21m,
    });
    Db.InvoiceItems.Add(new Domain.Finance.InvoiceItems.InvoiceItem
    {
      Id = Guid.NewGuid(),
      InvoiceId = invoiceId,
      ServiceGuid = serviceId,
      Quantity = 1,
      UnitPrice = 112m,
      VatRatePercentage = 12m,
    });

    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(new byte[] { 0xAA }));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);
    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);

    // Slice the HTML to just the per-invoice deduction block. The bill VAT recap follows.
    int sliceStart = html.IndexOf("F-INV-1", StringComparison.Ordinal);
    int sliceEnd = html.IndexOf("Rekapitulace DPH", sliceStart + 1, StringComparison.Ordinal);
    if (sliceEnd < 0)
    {
      sliceEnd = html.Length;
    }
    string invoiceBlock = html.Substring(sliceStart, sliceEnd - sliceStart);

    invoiceBlock.ShouldContain(">12 %<");
    invoiceBlock.ShouldContain(">21 %<");
    invoiceBlock.ShouldNotContain(">15 %<");  // active but unused on this invoice
  }

  [Fact]
  public async Task RenderAsync_BillWithLegalEntity_HidesPayerBlockAndLabelsLegalEntityAsPayer()
  {
    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();
    Db.Languages.Add(new Language { Id = languageId, Code = "cs-CZ", Name = "Czech" });

    Bill bill = new()
    {
      Id = billId,
      Number = "B-PARTY-1",
      Kind = BillKind.Regular,
      LanguageIdGuid = languageId,
      IssuedAtUtc = new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 5, 17),
      CheckOutAt = new DateOnly(2026, 5, 19),
      Payer = new Payer { Name = "PERSONNAMEXX", Surname = "PERSONSURNAMEXX", Address = MinAddress() },
      LegalEntity = new LegalEntity { Name = "COMPANYNAMEXX", Address = MinAddress(), Cin = "99999999", Tin = "CZ99999999" },
      Payment = new Payment(PaymentType.Cash, 100m),
    };
    Db.Bills.Add(bill);
    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(new byte[] { 0xAA }));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);
    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);

    html.ShouldContain("COMPANYNAMEXX");
    html.ShouldContain("Plátce");
    html.ShouldNotContain("Vystavitel");
    html.ShouldNotContain("PERSONNAMEXX");
    html.ShouldNotContain("PERSONSURNAMEXX");
  }

  [Fact]
  public async Task RenderAsync_BillWithoutLegalEntity_ShowsPayerBlock()
  {
    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();
    Db.Languages.Add(new Language { Id = languageId, Code = "cs-CZ", Name = "Czech" });

    Bill bill = new()
    {
      Id = billId,
      Number = "B-PARTY-2",
      Kind = BillKind.Regular,
      LanguageIdGuid = languageId,
      IssuedAtUtc = new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 5, 17),
      CheckOutAt = new DateOnly(2026, 5, 19),
      Payer = new Payer { Name = "INDIVIDUALNAMEXX", Surname = "INDIVIDUALSURNAMEXX", Address = MinAddress() },
      Payment = new Payment(PaymentType.Cash, 100m),
    };
    Db.Bills.Add(bill);
    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(new byte[] { 0xAA }));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);
    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);

    html.ShouldContain("INDIVIDUALNAMEXX");
    html.ShouldContain("INDIVIDUALSURNAMEXX");
    html.ShouldContain("Plátce");
  }

  [Theory]
  [InlineData(PaymentType.Cash, "Hotově")]
  [InlineData(PaymentType.Card, "Kartou")]
  public async Task RenderAsync_LocalizesPaymentMethod_Czech(PaymentType paymentType, string expectedCzechText)
  {
    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();
    Db.Languages.Add(new Language { Id = languageId, Code = "cs-CZ", Name = "Czech" });

    Bill bill = new()
    {
      Id = billId,
      Number = "B-PAY-1",
      Kind = BillKind.Regular,
      LanguageIdGuid = languageId,
      IssuedAtUtc = new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 5, 17),
      CheckOutAt = new DateOnly(2026, 5, 19),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = MinAddress() },
      Payment = new Payment(paymentType, 100m),
    };
    Db.Bills.Add(bill);
    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(new byte[] { 0xAA }));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);
    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);

    html.ShouldContain(expectedCzechText);
  }

  [Fact]
  public async Task RenderAsync_LinkedInvoice_DoesNotRenderPerLineItemTable()
  {
    var serviceId = Guid.NewGuid();
    Db.Services.Add(new Domain.Services.Services.Service
    {
      Id = serviceId,
      ServiceGroup = Domain.Services.Services.ServiceGroup.Spots,
      ServiceTypeId = Guid.NewGuid(),
      VatRateId = Guid.NewGuid(),
      Name = "Místo",
      IsActive = true,
    });

    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();
    Db.Languages.Add(new Language { Id = languageId, Code = "cs-CZ", Name = "Czech" });

    Bill bill = new()
    {
      Id = billId,
      Number = "B-DED-2",
      Kind = BillKind.Regular,
      LanguageIdGuid = languageId,
      IssuedAtUtc = new DateTime(2026, 5, 19, 10, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 5, 17),
      CheckOutAt = new DateOnly(2026, 5, 19),
      Payer = new Payer { Name = "John", Surname = "Doe", Address = MinAddress() },
      Payment = new Payment(PaymentType.Cash, 0m),
    };
    Db.Bills.Add(bill);

    var invoiceId = Guid.NewGuid();
    Db.Invoices.Add(new Domain.Finance.Invoices.Invoice
    {
      Id = invoiceId,
      ReservationId = Guid.NewGuid(),
      Number = "F-INV-2",
      Status = Domain.Finance.Invoices.InvoiceStatus.Paid,
      IssuedAt = new DateOnly(2026, 5, 1),
      Email = "",
      PhoneNumber = "",
      LinkedBillId = billId,
    });
    Db.InvoiceItems.Add(new Domain.Finance.InvoiceItems.InvoiceItem
    {
      Id = Guid.NewGuid(),
      InvoiceId = invoiceId,
      ServiceGuid = serviceId,
      Quantity = 1,
      UnitPrice = 121m,
      VatRatePercentage = 21m,
    });

    await Db.SaveChangesAsync();

    string capturedHtml = string.Empty;
    IPdfRenderer pdfRenderer = Substitute.For<IPdfRenderer>();
    pdfRenderer
      .RenderAsync(Arg.Do<string>(h => capturedHtml = h), Arg.Any<CancellationToken>())
      .Returns(Task.FromResult(new byte[] { 0xAA }));

    Result<BillDocumentRenderResult> result = await CreateSut(pdfRenderer).RenderAsync(billId, CancellationToken.None);
    result.IsSuccess.ShouldBeTrue();
    string html = System.Net.WebUtility.HtmlDecode(capturedHtml);

    // "Popis" (Description) header appears exactly once - only in the bill items table.
    // Per-invoice blocks no longer render a description column.
    int popisCount = System.Text.RegularExpressions.Regex.Count(html, "Popis");
    popisCount.ShouldBe(1);
  }
}
