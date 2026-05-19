using Application.Reservations.Guests;
using FluentValidation;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Reservations.Guests;

public sealed class SignatureValidationExtensionsTests
{
  private sealed record Holder(string? SignaturePngBase64);

  private sealed class HolderValidator : AbstractValidator<Holder>
  {
    public HolderValidator()
    {
      RuleFor(h => h.SignaturePngBase64).ValidPngBase64();
    }
  }

  private static readonly byte[] PngMagic =
    [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

  private static string MakePng(int payloadLength)
  {
    byte[] bytes = new byte[PngMagic.Length + payloadLength];
    Array.Copy(PngMagic, bytes, PngMagic.Length);
    return Convert.ToBase64String(bytes);
  }

  private readonly HolderValidator _sut = new();

  [Fact]
  public void NullSignature_IsValid()
  {
    _sut.TestValidate(new Holder(null))
      .ShouldNotHaveValidationErrorFor(h => h.SignaturePngBase64);
  }

  [Fact]
  public void ValidSmallPng_IsValid()
  {
    _sut.TestValidate(new Holder(MakePng(64)))
      .ShouldNotHaveValidationErrorFor(h => h.SignaturePngBase64);
  }

  [Fact]
  public void NonPngBytes_IsInvalid()
  {
    string base64 = Convert.ToBase64String([0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08]);
    _sut.TestValidate(new Holder(base64))
      .ShouldHaveValidationErrorFor(h => h.SignaturePngBase64);
  }

  [Fact]
  public void NotBase64_IsInvalid()
  {
    _sut.TestValidate(new Holder("!!!not-base64!!!"))
      .ShouldHaveValidationErrorFor(h => h.SignaturePngBase64);
  }

  [Fact]
  public void OverOneMegabyte_IsInvalid()
  {
    // 1 MiB + 1 of bytes -> definitely over the cap
    _sut.TestValidate(new Holder(MakePng(1_048_576)))
      .ShouldHaveValidationErrorFor(h => h.SignaturePngBase64);
  }
}
