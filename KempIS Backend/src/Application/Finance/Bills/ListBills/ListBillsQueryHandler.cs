using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Bills.ListBills;

internal sealed class ListBillsQueryHandler(IApplicationDbContext context)
  : IQueryHandler<ListBillsQuery, IReadOnlyList<BillSummary>>
{
  public async Task<Result<IReadOnlyList<BillSummary>>> Handle(
    ListBillsQuery query, CancellationToken cancellationToken)
  {
    IQueryable<Domain.Finance.Bills.Bill> bills = context.Bills.AsNoTracking();

    if (query.From.HasValue)
    {
      DateOnly from = query.From.Value;
      bills = bills.Where(b => b.CheckOutAt >= from);
    }

    if (query.To.HasValue)
    {
      DateOnly to = query.To.Value;
      bills = bills.Where(b => b.CheckInAt <= to);
    }

    if (query.ReservationId.HasValue)
    {
      bills = bills.Where(b => b.ReservationId == query.ReservationId.Value);
    }

    if (query.Kind.HasValue)
    {
      bills = bills.Where(b => b.Kind == query.Kind.Value);
    }

    if (query.FinancialClosingId.HasValue)
    {
      bills = bills.Where(b => b.FinancialClosingId == query.FinancialClosingId.Value);
    }

    if (query.Closed.HasValue)
    {
      bills = query.Closed.Value
        ? bills.Where(b => b.FinancialClosingId != null)
        : bills.Where(b => b.FinancialClosingId == null);
    }

    List<BillSummary> list = await bills
      .OrderByDescending(b => b.IssuedAtUtc)
      .Select(b => new BillSummary(
        b.Id, b.Number, b.Kind, b.ReservationId,
        b.CheckInAt, b.CheckOutAt, b.IssuedAtUtc, b.Payment.Amount,
        b.FinancialClosingId, b.Payment.PaymentType))
      .ToListAsync(cancellationToken);

    return Result.Success<IReadOnlyList<BillSummary>>(list);
  }
}
