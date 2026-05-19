using Application.Abstractions.Messaging;

namespace Application.Reservations.Commands.CancelReservationForGuest;

public sealed record CancelReservationForGuestCommand(Guid Id, string Secret) : ICommand;
