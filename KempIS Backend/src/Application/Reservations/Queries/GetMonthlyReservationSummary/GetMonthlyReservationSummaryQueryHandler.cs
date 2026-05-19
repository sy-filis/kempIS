using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Reservations.ReservationStates;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Queries.GetMonthlyReservationSummary;

internal sealed class GetMonthlyReservationSummaryQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetMonthlyReservationSummaryQuery, MonthlyReservationSummaryResponse>
{
  public async Task<Result<MonthlyReservationSummaryResponse>> Handle(
    GetMonthlyReservationSummaryQuery query,
    CancellationToken cancellationToken)
  {
    DateOnly yearStart = new(query.Year, 1, 1);
    DateOnly yearEnd = new(query.Year, 12, 31);

    List<PeriodRow> rows = await context.Reservations
      .Where(r => r.State == ReservationState.Confirmed
               || r.State == ReservationState.CheckedIn
               || r.State == ReservationState.Completed)
      .Where(r => r.Period.From <= yearEnd && r.Period.To >= yearStart)
      .Select(r => new PeriodRow(r.Period.From, r.Period.To))
      .ToListAsync(cancellationToken);

    int[] months = new int[12];

    foreach (PeriodRow row in rows)
    {
      DateOnly clampedFrom = row.From < yearStart ? yearStart : row.From;
      DateOnly clampedTo = row.To > yearEnd ? yearEnd : row.To;

      for (int month = clampedFrom.Month; month <= clampedTo.Month; month++)
      {
        months[month - 1]++;
      }
    }

    return new MonthlyReservationSummaryResponse(query.Year, months);
  }

  private readonly record struct PeriodRow(DateOnly From, DateOnly To);
}
