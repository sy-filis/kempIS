namespace Application.Reservations.Commands.CreateReservation;

// On create, Id is ignored. On update, non-null Id keeps and updates that row; null Id creates a new one.
public sealed record ReservationVehicleLine(
  Guid? Id,
  string RegistrationNumber);
