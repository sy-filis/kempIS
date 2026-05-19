using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Reservations.ReservationStates;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Queries.Stats.GetOccupancyStats;

internal sealed class GetOccupancyStatsQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetOccupancyStatsQuery, OccupancyStatsResponse>
{
  public async Task<Result<OccupancyStatsResponse>> Handle(
    GetOccupancyStatsQuery query,
    CancellationToken cancellationToken)
  {
    int nightsInRange = query.To.DayNumber - query.From.DayNumber + 1;

    List<ProjectedItem> items = await (
      from rsi in context.ReservationSpotItems.AsNoTracking()
      join r in context.Reservations on rsi.ReservationId equals r.Id
      join sg in context.SpotGroups on rsi.SpotGroupId equals sg.Id
      where (r.State == ReservationState.Confirmed
          || r.State == ReservationState.CheckedIn
          || r.State == ReservationState.Completed)
         && r.Period.From <= query.To
         && r.Period.To >= query.From
      select new ProjectedItem(
          sg.Id, sg.Name, sg.IsActive, sg.Capacity,
          r.Period.From, r.Period.To)
    ).ToListAsync(cancellationToken);

    int fromDay = query.From.DayNumber;
    int toExclusive = query.To.DayNumber + 1;

    var rows = items
      .GroupBy(i => new { i.SpotGroupId, i.Name, i.IsActive, i.Capacity })
      .Select(g =>
      {
        int occupied = 0;
        foreach (ProjectedItem item in g)
        {
          occupied += Math.Max(0,
            Math.Min(item.PeriodTo.DayNumber, toExclusive) -
            Math.Max(item.PeriodFrom.DayNumber, fromDay));
        }
        int capacity = (int)g.Key.Capacity * nightsInRange;
        decimal percent = capacity == 0
          ? 0m
          : Math.Round(100m * occupied / capacity, 1, MidpointRounding.AwayFromZero);
        return new OccupancyStatsRow(
          g.Key.SpotGroupId, g.Key.Name, g.Key.IsActive, g.Key.Capacity,
          occupied, capacity, percent);
      })
      .OrderByDescending(r => r.OccupancyPercent)
      .ThenBy(r => r.Name, StringComparer.Ordinal)
      .ToList();

    int totalOccupied = rows.Sum(r => r.OccupiedSpotNights);
    int totalCapacity = rows.Sum(r => r.CapacitySpotNights);
    decimal totalPercent = totalCapacity == 0
      ? 0m
      : Math.Round(100m * totalOccupied / totalCapacity, 1, MidpointRounding.AwayFromZero);

    return new OccupancyStatsResponse(
      query.From, query.To, nightsInRange,
      totalOccupied, totalCapacity, totalPercent, rows);
  }

  private readonly record struct ProjectedItem(
    Guid SpotGroupId,
    string Name,
    bool IsActive,
    uint Capacity,
    DateOnly PeriodFrom,
    DateOnly PeriodTo);
}
