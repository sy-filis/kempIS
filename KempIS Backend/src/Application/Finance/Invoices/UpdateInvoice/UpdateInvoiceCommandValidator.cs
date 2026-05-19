using FluentValidation;

namespace Application.Finance.Invoices.UpdateInvoice;

internal sealed class UpdateInvoiceCommandValidator : AbstractValidator<UpdateInvoiceCommand>
{
  public UpdateInvoiceCommandValidator()
  {
    RuleFor(c => c.InvoiceId).NotEmpty();

    RuleFor(c => c)
      .Must(c => (c.Payer is null) ^ (c.LegalEntity is null))
      .WithMessage("Invoice must have either a Payer or a LegalEntity, but not both.");

    When(c => c.Payer is not null, () =>
    {
      RuleFor(c => c.Payer!.Name).NotEmpty().MaximumLength(255);
      RuleFor(c => c.Payer!.Surname).NotEmpty().MaximumLength(255);
    });

    When(c => c.LegalEntity is not null, () =>
    {
      RuleFor(c => c.LegalEntity!.Name).NotEmpty().MaximumLength(255);
      RuleFor(c => c.LegalEntity!.Cin).NotEmpty();
    });

    RuleFor(c => c.Email).NotEmpty().EmailAddress().MaximumLength(320);
    RuleFor(c => c.PhoneNumber).NotEmpty().MaximumLength(32);

    RuleFor(c => c.Items).NotEmpty();
    RuleForEach(c => c.Items).ChildRules(item =>
    {
      item.RuleFor(i => i.ServiceGuid).NotEmpty();
      item.RuleFor(i => i.Quantity).GreaterThan(0);
      item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0);
      item.RuleFor(i => i.VatRatePercentage).InclusiveBetween(0, 100);
    });
  }
}
