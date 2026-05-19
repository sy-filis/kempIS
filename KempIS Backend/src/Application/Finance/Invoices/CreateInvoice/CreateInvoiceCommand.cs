using Application.Abstractions.Messaging;
using Application.Finance.Invoices.Shared;

namespace Application.Finance.Invoices.CreateInvoice;

public sealed record CreateInvoiceCommand(
  Guid ReservationId,
  InvoicePayerInput? Payer,
  InvoiceLegalEntityInput? LegalEntity,
  string Email,
  string PhoneNumber,
  IReadOnlyList<InvoiceItemInput> Items,
  DateOnly? DueTo = null)
  : ICommand<CreateInvoiceResponse>;

public sealed record CreateInvoiceResponse(Guid InvoiceId);
