using Application.Abstractions.Messaging;
using Domain.Finance.Invoices;

namespace Application.Finance.Invoices.ListInvoices;

public sealed record ListInvoicesQuery(
  DateTime? From,
  DateTime? To,
  Guid? ReservationId,
  InvoiceStateFilter? State)
  : IQuery<IReadOnlyList<InvoiceSummary>>;

// AfterDue matches: Status = Created AND DueTo has passed.
public enum InvoiceStateFilter
{
  Draft,
  Created,
  Paid,
  AfterDue,
}

public sealed record InvoiceSummary(
  Guid Id,
  InvoiceReservationOverview Reservation,
  string? Number,
  InvoiceStatus Status,
  DateOnly IssuedAt,
  DateOnly? PaidAt,
  DateOnly? DueTo,
  Guid? LinkedBillId,
  decimal TotalAmount);

public sealed record InvoiceReservationOverview(
  Guid Id,
  string Number,
  DateOnly From,
  DateOnly To);
