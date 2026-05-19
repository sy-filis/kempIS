using FluentValidation;

namespace Application.Finance.Invoices.DeleteInvoice;

internal sealed class DeleteInvoiceCommandValidator : AbstractValidator<DeleteInvoiceCommand>
{
  public DeleteInvoiceCommandValidator()
  {
    RuleFor(c => c.InvoiceId).NotEmpty();
  }
}
