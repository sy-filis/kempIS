using Application.Abstractions.Messaging;
using FluentValidation;

namespace Application.Finance.Invoices.MarkInvoiceCreated;

public sealed record MarkInvoiceCreatedCommand(
  Guid InvoiceId,
  string Number,
  DateOnly IssuedAt,
  DateOnly DueTo) : ICommand;

internal sealed class MarkInvoiceCreatedCommandValidator
  : AbstractValidator<MarkInvoiceCreatedCommand>
{
  public MarkInvoiceCreatedCommandValidator()
  {
    RuleFor(c => c.InvoiceId).NotEmpty();
    RuleFor(c => c.Number).NotEmpty().MaximumLength(50);
    RuleFor(c => c.DueTo).GreaterThanOrEqualTo(c => c.IssuedAt)
      .WithMessage("DueTo must be on or after IssuedAt.");
  }
}
