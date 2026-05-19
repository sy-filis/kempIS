using FluentValidation;

namespace Application.Finance.Queries.Stats.GetRevenueByPaymentMethod;

internal sealed class GetRevenueByPaymentMethodQueryValidator
  : AbstractValidator<GetRevenueByPaymentMethodQuery>
{
  public GetRevenueByPaymentMethodQueryValidator()
  {
    RuleFor(q => q.From).NotEmpty();
    RuleFor(q => q.To)
      .NotEmpty()
      .GreaterThanOrEqualTo(q => q.From)
      .Must((q, to) => to.DayNumber - q.From.DayNumber <= 365)
        .WithMessage("Range must not exceed 366 days.");
  }
}
