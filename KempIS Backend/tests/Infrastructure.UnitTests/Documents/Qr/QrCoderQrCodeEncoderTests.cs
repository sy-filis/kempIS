using Infrastructure.Documents.Qr;

namespace Infrastructure.UnitTests.Documents.Qr;

public sealed class QrCoderQrCodeEncoderTests
{
  [Fact]
  public void EncodePng_ReturnsValidPngBytes()
  {
    QrCoderQrCodeEncoder encoder = new();

    byte[] png = encoder.EncodePng("""{"billId":"3f2504e0-4f89-41d3-9a0c-0305e82c3301","checkOut":"2026-04-21"}""");

    png.Length.ShouldBeGreaterThan(100);
    png[0].ShouldBe((byte)0x89);
    png[1].ShouldBe((byte)0x50);  // 'P'
    png[2].ShouldBe((byte)0x4E);  // 'N'
    png[3].ShouldBe((byte)0x47);  // 'G'
  }

  [Fact]
  public void EncodePng_ShortAndLongPayloads_BothSucceed()
  {
    QrCoderQrCodeEncoder encoder = new();

    byte[] shortQr = encoder.EncodePng("abc");
    byte[] longQr = encoder.EncodePng(new string('x', 500));

    shortQr.Length.ShouldBeGreaterThan(0);
    longQr.Length.ShouldBeGreaterThan(shortQr.Length);
  }
}
