using Application.Abstractions.Messaging;

namespace Application.Reservations.Queries.GetAvailability;

public sealed record GetAvailabilityQuery(
  DateOnly From,
  DateOnly To,
  Guid? GroupReservationId = null,
  string? GroupReservationSecret = null) : IQuery<AvailabilityResponse>;
