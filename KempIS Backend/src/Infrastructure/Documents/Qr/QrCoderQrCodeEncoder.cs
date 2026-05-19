using Application.Abstractions.Documents;
using QRCoder;

namespace Infrastructure.Documents.Qr;

internal sealed class QrCoderQrCodeEncoder : IQrCodeEncoder
{
  public byte[] EncodePng(string payload)
  {
    using QRCodeGenerator generator = new();
    using QRCodeData data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
    using PngByteQRCode png = new(data);
    return png.GetGraphic(pixelsPerModule: 20);
  }
}
