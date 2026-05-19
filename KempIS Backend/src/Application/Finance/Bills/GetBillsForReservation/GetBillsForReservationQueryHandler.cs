using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Finance.Bills.ListBills;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Bills.GetBillsForReservation;

internal sealed class GetBillsForReservationQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetBillsForReservationQuery, IReadOnlyList<BillSummary>>
{
  public async Task<Result<IReadOnlyList<BillSummary>>> Handle(
    GetBillsForReservationQuery query,
    CancellationToken cancellationToken)
  {
    List<BillSummary> list = await context.Bills
      .AsNoTracking()
      .Where(b => b.ReservationId == query.ReservationId)
      .OrderByDescending(b => b.IssuedAtUtc)
      .Select(b => new BillSummary(
        b.Id, b.Number, b.Kind, b.ReservationId,
        b.CheckInAt, b.CheckOutAt, b.IssuedAtUtc, b.Payment.Amount,
        b.FinancialClosingId, b.Payment.PaymentType))
      .ToListAsync(cancellationToken);

    return Result.Success<IReadOnlyList<BillSummary>>(list);
  }
}
