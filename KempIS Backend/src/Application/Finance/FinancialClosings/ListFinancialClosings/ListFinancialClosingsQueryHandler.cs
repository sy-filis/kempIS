using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.FinancialClosings.ListFinancialClosings;

internal sealed class ListFinancialClosingsQueryHandler(IApplicationDbContext context)
  : IQueryHandler<ListFinancialClosingsQuery, IReadOnlyList<FinancialClosingSummary>>
{
  public async Task<Result<IReadOnlyList<FinancialClosingSummary>>> Handle(
    ListFinancialClosingsQuery query,
    CancellationToken cancellationToken)
  {
    var fromUtc = query.From?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    var toUtc = query.To?.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

    List<FinancialClosingSummary> results = await context.FinancialClosings
      .AsNoTracking()
      .Where(c => fromUtc == null || c.ClosedAtUtc >= fromUtc)
      .Where(c => toUtc == null || c.ClosedAtUtc <= toUtc)
      .OrderByDescending(c => c.ClosedAtUtc)
      .Select(c => new FinancialClosingSummary(
        c.Id,
        c.FinancialClosingId,
        c.ClosedAtUtc,
        c.TotalAmount,
        context.Bills.Count(b => b.FinancialClosingId == c.Id),
        c.CreatedByUserId))
      .ToListAsync(cancellationToken);

    return results;
  }
}
