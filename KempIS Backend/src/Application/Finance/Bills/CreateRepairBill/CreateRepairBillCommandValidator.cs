using FluentValidation;

namespace Application.Finance.Bills.CreateRepairBill;

internal sealed class CreateRepairBillCommandValidator : AbstractValidator<CreateRepairBillCommand>
{
  public CreateRepairBillCommandValidator()
  {
    RuleFor(c => c.OriginalBillId).NotEmpty();
    RuleFor(c => c.Reason).NotEmpty().MaximumLength(500);
    RuleFor(c => c.Items).NotEmpty();
    RuleForEach(c => c.Items).ChildRules(item =>
    {
      item.RuleFor(i => i.RecapSingleQuantity).GreaterThan(0u);
      item.RuleFor(i => i.RecapDayQuantity).GreaterThan(0u);
      item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0m);
      item.RuleFor(i => i.VatRatePercentage).InclusiveBetween(0m, 100m);
    });
  }
}
