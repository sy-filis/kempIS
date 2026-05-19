namespace Application.Reservations.Queries.GetAvailability;

public sealed record AvailabilityResponse(
  IReadOnlyList<SpotGroupAvailability> SpotGroups);

public sealed record SpotGroupAvailability(
  Guid SpotGroupId,
  string Name,
  uint Capacity,
  int TotalSpots,
  int Occupied,
  int Available,
  string ImageUrl,
  string DetailsUrl,
  IReadOnlyList<SpotGroupEvent> Events);

public sealed record SpotGroupEvent(
  Guid EventId,
  string Name,
  string? Description,
  DateOnly StartsAt,
  DateOnly? EndsAt);
