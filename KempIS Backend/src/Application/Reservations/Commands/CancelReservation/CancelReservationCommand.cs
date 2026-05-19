using Application.Abstractions.Messaging;

namespace Application.Reservations.Commands.CancelReservation;

public sealed record CancelReservationCommand(Guid Id) : ICommand;
