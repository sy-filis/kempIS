using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.Invoices;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Invoices.ListInvoices;

internal sealed class ListInvoicesQueryHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider)
  : IQueryHandler<ListInvoicesQuery, IReadOnlyList<InvoiceSummary>>
{
  public async Task<Result<IReadOnlyList<InvoiceSummary>>> Handle(
    ListInvoicesQuery query,
    CancellationToken cancellationToken)
  {
    IQueryable<Domain.Finance.Invoices.Invoice> invoices = context.Invoices.AsNoTracking();

    if (query.From.HasValue || query.To.HasValue)
    {
      DateOnly? fromDate = query.From.HasValue ? DateOnly.FromDateTime(query.From.Value) : null;
      DateOnly? toDate = query.To.HasValue ? DateOnly.FromDateTime(query.To.Value) : null;

      invoices = invoices.Where(i => context.Reservations.Any(r =>
        r.Id == i.ReservationId
        && (!fromDate.HasValue || r.Period.To >= fromDate.Value)
        && (!toDate.HasValue || r.Period.From <= toDate.Value)));
    }

    if (query.ReservationId.HasValue)
    {
      invoices = invoices.Where(i => i.ReservationId == query.ReservationId.Value);
    }

    if (query.State.HasValue)
    {
      switch (query.State.Value)
      {
        case InvoiceStateFilter.Draft:
          invoices = invoices.Where(i => i.Status == InvoiceStatus.Draft);
          break;
        case InvoiceStateFilter.Created:
          invoices = invoices.Where(i => i.Status == InvoiceStatus.Created);
          break;
        case InvoiceStateFilter.Paid:
          invoices = invoices.Where(i => i.Status == InvoiceStatus.Paid);
          break;
        case InvoiceStateFilter.AfterDue:
          var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
          invoices = invoices.Where(i =>
            i.Status == InvoiceStatus.Created
            && i.DueTo.HasValue
            && i.DueTo.Value < today);
          break;
      }
    }

    List<InvoiceSummary> list = await invoices
      .OrderByDescending(i => i.IssuedAt)
      .Select(i => new InvoiceSummary(
        i.Id,
        context.Reservations
          .Where(r => r.Id == i.ReservationId)
          .Select(r => new InvoiceReservationOverview(r.Id, r.Number, r.Period.From, r.Period.To))
          .First(),
        i.Number, i.Status, i.IssuedAt, i.PaidAt, i.DueTo, i.LinkedBillId,
        context.InvoiceItems
          .Where(item => item.InvoiceId == i.Id)
          .Sum(item => item.Quantity * item.UnitPrice)))
      .ToListAsync(cancellationToken);

    return Result.Success<IReadOnlyList<InvoiceSummary>>(list);
  }
}
