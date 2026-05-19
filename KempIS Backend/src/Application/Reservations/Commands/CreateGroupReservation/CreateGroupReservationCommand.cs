using Application.Abstractions.Messaging;

namespace Application.Reservations.Commands.CreateGroupReservation;

public sealed record CreateGroupReservationCommand(
  DateOnly From,
  DateOnly To,
  IReadOnlyList<Guid> SpotIds,
  string OrganizerName,
  string OrganizerEmail,
  string OrganizerPhone,
  string? Note,
  string Language,
  string? DisplayName = null) : ICommand<CreateGroupReservationResponse>;

public sealed record CreateGroupReservationResponse(Guid Id, string Number, string Secret);
