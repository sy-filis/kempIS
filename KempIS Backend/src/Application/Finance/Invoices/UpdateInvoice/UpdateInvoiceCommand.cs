using Application.Abstractions.Messaging;
using Application.Finance.Invoices.Shared;

namespace Application.Finance.Invoices.UpdateInvoice;

public sealed record UpdateInvoiceCommand(
  Guid InvoiceId,
  InvoicePayerInput? Payer,
  InvoiceLegalEntityInput? LegalEntity,
  string Email,
  string PhoneNumber,
  IReadOnlyList<InvoiceItemInput> Items,
  DateOnly? DueTo = null)
  : ICommand;
