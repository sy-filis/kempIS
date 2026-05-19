using Application.Abstractions.Messaging;

namespace Application.Reservations.Commands.CreateWebReservation;

public sealed record CreateWebReservationCommand(
  string Name,
  string Surname,
  string Email,
  string Phone,
  DateOnly From,
  DateOnly To,
  IReadOnlyList<RequestedSpotGroup> RequestedSpots,
  string? Note,
  Guid? GroupReservationId,
  string? GroupReservationSecret,
  string? Language = null) : ICommand<CreateWebReservationResponse>;

public sealed record RequestedSpotGroup(Guid SpotGroupId, uint Quantity);

public sealed record CreateWebReservationResponse(Guid Id, string Number);
