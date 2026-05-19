using Application.Finance.Queries.Stats.GetRevenueByPaymentMethod;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Finance.Queries.Stats.GetRevenueByPaymentMethod;

public sealed class GetRevenueByPaymentMethodQueryValidatorTests
{
  private readonly GetRevenueByPaymentMethodQueryValidator _validator = new();

  [Fact]
  public void Valid_Passes()
      => _validator.TestValidate(new GetRevenueByPaymentMethodQuery(new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 31)))
          .ShouldNotHaveAnyValidationErrors();

  [Fact]
  public void ToBeforeFrom_Fails()
      => _validator.TestValidate(new GetRevenueByPaymentMethodQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 7, 1)))
          .ShouldHaveValidationErrorFor(q => q.To);

  [Fact]
  public void RangeOver366Days_Fails()
      => _validator.TestValidate(new GetRevenueByPaymentMethodQuery(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 2)))
          .ShouldHaveValidationErrorFor(q => q.To);
}
