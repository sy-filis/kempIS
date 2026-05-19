using Application.Abstractions.Messaging;
using FluentValidation;

namespace Application.Finance.Invoices.MarkInvoicePaid;

public sealed record MarkInvoicePaidCommand(Guid InvoiceId, DateOnly PaidAt) : ICommand;

internal sealed class MarkInvoicePaidCommandValidator
  : AbstractValidator<MarkInvoicePaidCommand>
{
  public MarkInvoicePaidCommandValidator()
  {
    RuleFor(c => c.InvoiceId).NotEmpty();
  }
}
