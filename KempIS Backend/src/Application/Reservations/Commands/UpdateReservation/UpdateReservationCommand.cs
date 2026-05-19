using Application.Abstractions.Messaging;
using Application.Reservations.Commands.CreateReservation;

namespace Application.Reservations.Commands.UpdateReservation;

public sealed record UpdateReservationCommand(
  Guid Id,
  string Name,
  string Surname,
  string Email,
  string Phone,
  DateOnly From,
  DateOnly To,
  string? Note,
  Guid? GroupReservationId,
  IReadOnlyList<Guid> SpotIds,
  IReadOnlyList<ReservationServiceLine> Services,
  IReadOnlyList<ReservationVehicleLine> Vehicles,
  string? DisplayName = null,
  string? Language = null) : ICommand;
