using Application.Abstractions.Messaging;

namespace Application.Reservations.Commands.UpdateGroupReservation;

public sealed record UpdateGroupReservationCommand(
  Guid Id,
  DateOnly From,
  DateOnly To,
  IReadOnlyList<Guid> SpotIds,
  string OrganizerName,
  string OrganizerEmail,
  string OrganizerPhone,
  string? Note,
  string? DisplayName = null) : ICommand;
