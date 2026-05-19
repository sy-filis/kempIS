using Application.Abstractions.Messaging;

namespace Application.Reservations.Commands.CreateReservation;

public sealed record CreateReservationCommand(
  string Name,
  string Surname,
  string Email,
  string Phone,
  DateOnly From,
  DateOnly To,
  IReadOnlyList<Guid> SpotIds,
  string? Note,
  Guid? GroupReservationId,
  IReadOnlyList<ReservationServiceLine> Services,
  IReadOnlyList<ReservationVehicleLine> Vehicles,
  string? DisplayName = null,
  string? Language = null) : ICommand<Guid>;
