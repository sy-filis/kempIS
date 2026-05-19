using Application.Finance.Queries.Stats.GetServiceRevenueStats;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Finance.Queries.Stats.GetServiceRevenueStats;

public sealed class GetServiceRevenueStatsQueryValidatorTests
{
  private readonly GetServiceRevenueStatsQueryValidator _validator = new();

  [Fact]
  public void Valid_Passes()
      => _validator.TestValidate(new GetServiceRevenueStatsQuery(new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 31)))
          .ShouldNotHaveAnyValidationErrors();

  [Fact]
  public void ToBeforeFrom_Fails()
      => _validator.TestValidate(new GetServiceRevenueStatsQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 7, 1)))
          .ShouldHaveValidationErrorFor(q => q.To);

  [Fact]
  public void RangeOver366Days_Fails()
      => _validator.TestValidate(new GetServiceRevenueStatsQuery(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 2)))
          .ShouldHaveValidationErrorFor(q => q.To);

  [Fact]
  public void From_Default_Fails()
      => _validator.TestValidate(new GetServiceRevenueStatsQuery(default, new DateOnly(2026, 6, 1)))
          .ShouldHaveValidationErrorFor(q => q.From);

  [Fact]
  public void To_Default_Fails()
      => _validator.TestValidate(new GetServiceRevenueStatsQuery(new DateOnly(2026, 6, 1), default))
          .ShouldHaveValidationErrorFor(q => q.To);
}
