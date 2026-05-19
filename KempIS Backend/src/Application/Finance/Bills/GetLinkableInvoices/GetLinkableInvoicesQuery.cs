using Application.Abstractions.Messaging;

namespace Application.Finance.Bills.GetLinkableInvoices;

public sealed record GetLinkableInvoicesQuery(Guid ReservationId)
  : IQuery<IReadOnlyList<LinkableInvoiceView>>;

public sealed record LinkableInvoiceView(
  Guid Id,
  string Number,
  DateOnly IssuedAt,
  DateOnly? PaidAt,
  decimal TotalAmount);
