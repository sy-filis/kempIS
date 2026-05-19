using Application.Abstractions.Messaging;

namespace Application.Reservations.Queries.Stats.GetOccupancyStats;

public sealed record GetOccupancyStatsQuery(DateOnly From, DateOnly To)
  : IQuery<OccupancyStatsResponse>;

public sealed record OccupancyStatsResponse(
  DateOnly From,
  DateOnly To,
  int NightsInRange,
  int TotalOccupiedSpotNights,
  int TotalCapacitySpotNights,
  decimal TotalOccupancyPercent,
  IReadOnlyList<OccupancyStatsRow> Groups);

public sealed record OccupancyStatsRow(
  Guid SpotGroupId,
  string Name,
  bool IsActive,
  uint Capacity,
  int OccupiedSpotNights,
  int CapacitySpotNights,
  decimal OccupancyPercent);
