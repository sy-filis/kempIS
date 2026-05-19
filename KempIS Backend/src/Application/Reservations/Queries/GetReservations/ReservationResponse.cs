using Domain.Reservations.ReservationStates;

namespace Application.Reservations.Queries.GetReservations;

public sealed record ReservationResponse(
  Guid Id,
  string Number,
  string ReservationMakerName,
  string ReservationMakerSurname,
  string ReservationMakerEmail,
  string ReservationMakerPhone,
  Guid? GroupReservationId,
  DateOnly From,
  DateOnly To,
  ReservationState State,
  DateTime CreatedAtUtc,
  DateTime? UpdatedAtUtc,
  string? Note,
  IReadOnlyList<Guid> SpotItems,
  string? DisplayName);
