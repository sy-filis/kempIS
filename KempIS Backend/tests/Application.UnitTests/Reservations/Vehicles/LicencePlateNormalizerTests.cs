using Application.Reservations.Vehicles;

namespace Application.UnitTests.Reservations.Vehicles;

public sealed class LicencePlateNormalizerTests
{
  [Theory]
  [InlineData("1AB2345", "1AB2345")]
  [InlineData("1ab2345", "1AB2345")]
  [InlineData("1AB-2345", "1AB2345")]
  [InlineData("1AB 2345", "1AB2345")]
  [InlineData(" 1ab.2345 ", "1AB2345")]
  [InlineData("1A/B-23.45", "1AB2345")]
  [InlineData("1ÁB2345", "1B2345")]   // diacritic stripped, no transliteration
  [InlineData("ABC", "ABC")]
  [InlineData("123", "123")]
  public void Normalize_StripsSeparatorsAndUppercases(string input, string expected)
  {
    LicencePlateNormalizer.Normalize(input).ShouldBe(expected);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("---")]
  [InlineData("...")]
  public void Normalize_ReturnsEmpty_WhenInputHasNoAlphanumerics(string input)
  {
    LicencePlateNormalizer.Normalize(input).ShouldBe(string.Empty);
  }

  [Fact]
  public void Normalize_NullInput_Throws()
  {
    Should.Throw<ArgumentNullException>(() => LicencePlateNormalizer.Normalize(null!));
  }
}
