using FluentValidation;

namespace Application.Reservations.Guests;

internal static class SignatureValidationExtensions
{
  private const int MaxBytes = 1_048_576;
  private static ReadOnlySpan<byte> PngMagic =>
    [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

  public static IRuleBuilderOptions<T, string?> ValidPngBase64<T>(
    this IRuleBuilder<T, string?> rule)
    => rule.Must(BeValidPngBase64Within1Mb)
           .WithMessage("Signature must be a valid PNG image up to 1 MB.");

  private static bool BeValidPngBase64Within1Mb(string? base64)
  {
    if (base64 is null)
    { return true; }

    Span<byte> buffer = new byte[MaxBytes + 1];
    if (!Convert.TryFromBase64String(base64, buffer, out int written))
    {
      return false;
    }
    if (written > MaxBytes)
    { return false; }

    return written >= PngMagic.Length
        && buffer[..PngMagic.Length].SequenceEqual(PngMagic);
  }
}
