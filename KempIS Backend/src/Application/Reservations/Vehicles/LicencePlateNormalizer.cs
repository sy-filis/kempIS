using System.Text.RegularExpressions;

namespace Application.Reservations.Vehicles;

internal static partial class LicencePlateNormalizer
{
  [GeneratedRegex("[^A-Z0-9]", RegexOptions.CultureInvariant)]
  private static partial Regex NonAlphanumericUpper();

  public static string Normalize(string input)
  {
    ArgumentNullException.ThrowIfNull(input);
    return NonAlphanumericUpper().Replace(input.ToUpperInvariant(), string.Empty);
  }
}
