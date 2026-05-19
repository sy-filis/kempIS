using Application.Reservations.Queries.GetAvailability;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Reservations.Queries.GetAvailability;

public sealed class GetAvailabilityQueryValidatorTests
{
  private readonly GetAvailabilityQueryValidator _validator = new();

  private static GetAvailabilityQuery Valid()
      => new(new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 15));

  [Fact]
  public void Valid_Passes()
      => _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();

  [Fact]
  public void From_Default_Fails()
      => _validator.TestValidate(Valid() with { From = default })
          .ShouldHaveValidationErrorFor(q => q.From);

  [Fact]
  public void To_BeforeFrom_Fails()
      => _validator.TestValidate(Valid() with
      {
        From = new DateOnly(2026, 7, 15),
        To = new DateOnly(2026, 7, 10),
      })
          .ShouldHaveValidationErrorFor(q => q.To);

  [Fact]
  public void To_EqualToFrom_Passes()
      => _validator.TestValidate(Valid() with
      {
        From = new DateOnly(2026, 7, 10),
        To = new DateOnly(2026, 7, 10),
      })
          .ShouldNotHaveValidationErrorFor(q => q.To);
}
