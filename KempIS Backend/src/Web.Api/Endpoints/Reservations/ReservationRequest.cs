namespace Web.Api.Endpoints.Reservations;

internal sealed record ReservationRequest(
  string Name,
  string Surname,
  string Email,
  string Phone,
  DateOnly From,
  DateOnly To,
  string? Note,
  Guid? GroupReservationId,
  IReadOnlyList<Guid> SpotIds,
  IReadOnlyList<ReservationServiceRequest> Services,
  IReadOnlyList<ReservationVehicleRequest> Vehicles,
  string? DisplayName,
  string? Language);

internal sealed record ReservationServiceRequest(
  Guid ServiceId,
  uint Quantity,
  uint RecapSingleQuantity,
  uint RecapDayQuantity);

internal sealed record ReservationVehicleRequest(
  Guid? Id,
  string RegistrationNumber);
