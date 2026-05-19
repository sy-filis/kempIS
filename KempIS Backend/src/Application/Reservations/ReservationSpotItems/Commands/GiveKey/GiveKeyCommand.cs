using Application.Abstractions.Messaging;

namespace Application.Reservations.ReservationSpotItems.Commands.GiveKey;

public sealed record GiveKeyCommand(Guid Id) : ICommand;
