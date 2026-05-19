using Application.Abstractions.Documents;
using Application.Finance.Bills;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Infrastructure.Documents.Bills;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RazorLight;
using SharedKernel;

namespace Infrastructure.UnitTests.Documents.Bills;

public sealed class RazorLightBillStickerRendererTests
{
  private readonly IPdfRenderer _pdfRenderer = Substitute.For<IPdfRenderer>();
  private readonly IQrCodeEncoder _qr = Substitute.For<IQrCodeEncoder>();

  private RazorLightBillStickerRenderer CreateSut()
  {
    RazorLightEngine engine = new RazorLightEngineBuilder()
      .UseEmbeddedResourcesProject(typeof(BillStickerModel).Assembly, "Infrastructure.Documents")
      .UseMemoryCachingProvider()
      .Build();

    return new RazorLightBillStickerRenderer(
      _pdfRenderer,
      _qr,
      engine,
      NullLogger<RazorLightBillStickerRenderer>.Instance);
  }

  [Fact]
  public async Task RenderAsync_ReturnsBytes_AndEmbedsCheckoutDateInHtml()
  {
    Bill bill = NewBill(Guid.NewGuid(), checkOut: new DateOnly(2026, 4, 22));

    _qr.EncodePng(Arg.Any<string>()).Returns([1, 2, 3]);

    string? capturedHtml = null;
    _pdfRenderer
      .RenderAsync(
        Arg.Do<string>(h => capturedHtml = h),
        Arg.Any<PdfPageOptions>(),
        Arg.Any<CancellationToken>())
      .Returns(new byte[] { 0xAA });

    Result<byte[]> result = await CreateSut().RenderAsync(bill, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBe([0xAA]);
    capturedHtml.ShouldNotBeNull();
    capturedHtml!.ShouldContain("Odjezd:");
    capturedHtml!.ShouldContain("22. 4. 2026");
    capturedHtml!.ShouldContain("data:image/png;base64,");
  }

  [Fact]
  public async Task RenderAsync_EncodesJsonPayload_ViaQrEncoder()
  {
    var billId = Guid.NewGuid();
    Bill bill = NewBill(billId, checkOut: new DateOnly(2026, 4, 22));

    string? capturedPayload = null;
    _qr.EncodePng(Arg.Do<string>(p => capturedPayload = p)).Returns([1]);
    _pdfRenderer.RenderAsync(Arg.Any<string>(), Arg.Any<PdfPageOptions>(), Arg.Any<CancellationToken>())
      .Returns([0xAA]);

    await CreateSut().RenderAsync(bill, CancellationToken.None);

    capturedPayload.ShouldNotBeNull();
    capturedPayload!.ShouldContain($"\"billId\":\"{billId}\"");
    capturedPayload!.ShouldContain("\"checkOut\":\"2026-04-22\"");
  }

  [Fact]
  public async Task RenderAsync_UsesStickerPageSize()
  {
    Bill bill = NewBill(Guid.NewGuid(), checkOut: new DateOnly(2026, 4, 22));

    PdfPageOptions? capturedOptions = null;
    _qr.EncodePng(Arg.Any<string>()).Returns([1]);
    _pdfRenderer
      .RenderAsync(
        Arg.Any<string>(),
        Arg.Do<PdfPageOptions>(o => capturedOptions = o),
        Arg.Any<CancellationToken>())
      .Returns([0xAA]);

    await CreateSut().RenderAsync(bill, CancellationToken.None);

    capturedOptions.ShouldNotBeNull();
    capturedOptions!.Width.ShouldBe("62mm");
    capturedOptions!.Height.ShouldBe("19mm");
    capturedOptions!.MarginTop.ShouldBe("0");
    capturedOptions!.PrintBackground.ShouldBeTrue();
  }

  private static Address MinAddress() => new(
    Guid.NewGuid(),
    "Prague",
    "10000",
    "Main St",
    "1");

  private static Bill NewBill(Guid id, DateOnly checkOut) =>
    new()
    {
      Id = id,
      Number = "B-ST-001",
      ReservationId = Guid.NewGuid(),
      IssuedAtUtc = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc),
      CheckInAt = checkOut.AddDays(-2),
      CheckOutAt = checkOut,
      LanguageIdGuid = Guid.NewGuid(),
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
}
