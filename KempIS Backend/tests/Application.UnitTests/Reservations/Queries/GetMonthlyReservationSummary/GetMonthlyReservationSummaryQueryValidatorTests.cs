using Application.Reservations.Queries.GetMonthlyReservationSummary;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Reservations.Queries.GetMonthlyReservationSummary;

public sealed class GetMonthlyReservationSummaryQueryValidatorTests
{
  private readonly GetMonthlyReservationSummaryQueryValidator _validator = new();

  [Fact]
  public void Year_InRange_Passes()
      => _validator.TestValidate(new GetMonthlyReservationSummaryQuery(2026))
          .ShouldNotHaveAnyValidationErrors();

  [Fact]
  public void Year_LowerBoundary_Passes()
      => _validator.TestValidate(new GetMonthlyReservationSummaryQuery(2000))
          .ShouldNotHaveAnyValidationErrors();

  [Fact]
  public void Year_UpperBoundary_Passes()
      => _validator.TestValidate(new GetMonthlyReservationSummaryQuery(2100))
          .ShouldNotHaveAnyValidationErrors();

  [Fact]
  public void Year_BelowMinimum_Fails()
      => _validator.TestValidate(new GetMonthlyReservationSummaryQuery(1999))
          .ShouldHaveValidationErrorFor(q => q.Year);

  [Fact]
  public void Year_AboveMaximum_Fails()
      => _validator.TestValidate(new GetMonthlyReservationSummaryQuery(2101))
          .ShouldHaveValidationErrorFor(q => q.Year);

  [Fact]
  public void Year_Zero_Fails()
      => _validator.TestValidate(new GetMonthlyReservationSummaryQuery(0))
          .ShouldHaveValidationErrorFor(q => q.Year);
}
