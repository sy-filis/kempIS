using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Finance.Invoices;

namespace Application.Finance.Invoices.GetInvoiceById;

public sealed record GetInvoiceByIdQuery(Guid InvoiceId) : IQuery<GetInvoiceByIdResponse>;

public sealed record InvoicePayerView(string Name, string Surname, Address Address);

public sealed record InvoiceLegalEntityView(string Name, string Cin, string? Tin, Address Address);

public sealed record InvoiceItemView(
  Guid Id,
  Guid ServiceGuid,
  decimal Quantity,
  decimal UnitPrice,
  decimal VatRatePercentage);

public sealed record GetInvoiceByIdResponse(
  Guid Id,
  Guid ReservationId,
  string? Number,
  InvoiceStatus Status,
  DateOnly IssuedAt,
  DateOnly? PaidAt,
  DateOnly? DueTo,
  Guid? LinkedBillId,
  string Email,
  string PhoneNumber,
  InvoicePayerView? Payer,
  InvoiceLegalEntityView? LegalEntity,
  IReadOnlyList<InvoiceItemView> Items);
