using Application.Abstractions.Messaging;

namespace Application.Reservations.ReservationSpotItems.Commands.ReturnKeys;

public sealed record ReturnKeysCommand(Guid Id) : ICommand;
