using Application.Abstractions.Messaging;

namespace Application.Reservations.Commands.CheckInReservation;

public sealed record CheckInReservationCommand(Guid Id) : ICommand;
