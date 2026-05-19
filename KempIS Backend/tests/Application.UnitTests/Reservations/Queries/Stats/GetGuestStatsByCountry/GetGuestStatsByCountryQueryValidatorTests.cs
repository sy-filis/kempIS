using Application.Reservations.Queries.Stats.GetGuestStatsByCountry;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Reservations.Queries.Stats.GetGuestStatsByCountry;

public sealed class GetGuestStatsByCountryQueryValidatorTests
{
  private readonly GetGuestStatsByCountryQueryValidator _validator = new();

  [Fact]
  public void Valid_Passes()
      => _validator.TestValidate(new GetGuestStatsByCountryQuery(new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 31)))
          .ShouldNotHaveAnyValidationErrors();

  [Fact]
  public void FromEqualsTo_Passes()
      => _validator.TestValidate(new GetGuestStatsByCountryQuery(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1)))
          .ShouldNotHaveAnyValidationErrors();

  [Fact]
  public void From_Default_Fails()
      => _validator.TestValidate(new GetGuestStatsByCountryQuery(default, new DateOnly(2026, 6, 1)))
          .ShouldHaveValidationErrorFor(q => q.From);

  [Fact]
  public void To_Default_Fails()
      => _validator.TestValidate(new GetGuestStatsByCountryQuery(new DateOnly(2026, 6, 1), default))
          .ShouldHaveValidationErrorFor(q => q.To);

  [Fact]
  public void ToBeforeFrom_Fails()
      => _validator.TestValidate(new GetGuestStatsByCountryQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 7, 1)))
          .ShouldHaveValidationErrorFor(q => q.To);

  [Fact]
  public void ExactlyOneYearSpan_Passes()
      => _validator.TestValidate(new GetGuestStatsByCountryQuery(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)))
          .ShouldNotHaveAnyValidationErrors();

  [Fact]
  public void OverOneYearSpan_Fails()
      => _validator.TestValidate(new GetGuestStatsByCountryQuery(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 2)))
          .ShouldHaveValidationErrorFor(q => q.To);
}
