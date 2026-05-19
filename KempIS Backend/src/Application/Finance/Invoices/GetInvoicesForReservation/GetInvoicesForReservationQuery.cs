using Application.Abstractions.Messaging;
using Application.Finance.Invoices.ListInvoices;

namespace Application.Finance.Invoices.GetInvoicesForReservation;

public sealed record GetInvoicesForReservationQuery(Guid ReservationId)
  : IQuery<IReadOnlyList<InvoiceSummary>>;
