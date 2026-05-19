using Application.Abstractions.Messaging;

namespace Application.Reservations.Commands.SendGroupReservationInvitation;

public sealed record SendGroupReservationInvitationCommand(
  Guid GroupReservationId,
  string Language) : ICommand;
