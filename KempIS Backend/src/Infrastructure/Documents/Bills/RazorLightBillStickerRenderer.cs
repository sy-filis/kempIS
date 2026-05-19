using System.Text.Json;
using Application.Abstractions.Documents;
using Application.Finance.Bills;
using Domain.Finance.Bills;
using Microsoft.Extensions.Logging;
using RazorLight;
using SharedKernel;

namespace Infrastructure.Documents.Bills;

internal sealed class RazorLightBillStickerRenderer : IBillStickerRenderer
{
  private const string TemplateKey = "Bills.Templates.BillSticker.cshtml";

  private static readonly PdfPageOptions StickerOptions = new(
    Format: null,
    Width: "62mm",
    Height: "19mm",
    Landscape: false,
    MarginTop: "0",
    MarginBottom: "0",
    MarginLeft: "0",
    MarginRight: "0",
    PrintBackground: true);

  // Null naming policy preserves anonymous-object PascalCase; a shared options instance would break the QR shape.
  private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = null };

  private readonly IPdfRenderer _pdfRenderer;
  private readonly IQrCodeEncoder _qr;
  private readonly RazorLightEngine _engine;
  private readonly ILogger<RazorLightBillStickerRenderer> _logger;

  public RazorLightBillStickerRenderer(
    IPdfRenderer pdfRenderer,
    IQrCodeEncoder qr,
    RazorLightEngine engine,
    ILogger<RazorLightBillStickerRenderer> logger)
  {
    _pdfRenderer = pdfRenderer;
    _qr = qr;
    _engine = engine;
    _logger = logger;
  }

  public async Task<Result<byte[]>> RenderAsync(Bill bill, CancellationToken cancellationToken)
  {
    if (_logger.IsEnabled(LogLevel.Debug))
    {
      _logger.LogDebug("Rendering sticker for bill {BillId}", bill.Id);
    }

    string payload = JsonSerializer.Serialize(
      new { billId = bill.Id, checkOut = bill.CheckOutAt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) },
      JsonOptions);

    byte[] qrPng = _qr.EncodePng(payload);
    string qrDataUrl = "data:image/png;base64," + Convert.ToBase64String(qrPng);

    BillStickerModel model = new(qrDataUrl, bill.CheckOutAt);
    string html = await _engine.CompileRenderAsync(TemplateKey, model);

    byte[] pdf = await _pdfRenderer.RenderAsync(html, StickerOptions, cancellationToken);

    return Result.Success(pdf);
  }
}
