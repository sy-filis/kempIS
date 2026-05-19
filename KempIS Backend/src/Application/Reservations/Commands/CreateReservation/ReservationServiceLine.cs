namespace Application.Reservations.Commands.CreateReservation;

public sealed record ReservationServiceLine(
  Guid ServiceId,
  uint Quantity,
  uint RecapSingleQuantity,
  uint RecapDayQuantity);
