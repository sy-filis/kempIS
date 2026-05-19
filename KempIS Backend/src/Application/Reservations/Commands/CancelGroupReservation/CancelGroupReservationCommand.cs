using Application.Abstractions.Messaging;

namespace Application.Reservations.Commands.CancelGroupReservation;

public sealed record CancelGroupReservationCommand(Guid Id) : ICommand;
