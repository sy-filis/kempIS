using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Finance.Invoices.ListInvoices;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Invoices.GetInvoicesForReservation;

internal sealed class GetInvoicesForReservationQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetInvoicesForReservationQuery, IReadOnlyList<InvoiceSummary>>
{
  public async Task<Result<IReadOnlyList<InvoiceSummary>>> Handle(
    GetInvoicesForReservationQuery query,
    CancellationToken cancellationToken)
  {
    List<InvoiceSummary> list = await context.Invoices
      .AsNoTracking()
      .Where(i => i.ReservationId == query.ReservationId)
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
