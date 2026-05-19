using Application.Reservations.Guests;

namespace Application.UnitTests.Reservations.Guests;

public sealed class GuestSignatureRulesTests
{
  [Theory]
  [InlineData("CZ", false)]
  [InlineData("DE", true)]
  [InlineData("SK", true)]
  [InlineData("", true)]
  public void RequiresSignature_ReturnsExpected(string alpha2, bool expected)
  {
    GuestSignatureRules.RequiresSignature(alpha2).ShouldBe(expected);
  }
}
