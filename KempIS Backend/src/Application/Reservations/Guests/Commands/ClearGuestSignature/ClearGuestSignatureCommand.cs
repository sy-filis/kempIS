using Application.Abstractions.Messaging;

namespace Application.Reservations.Guests.Commands.ClearGuestSignature;

public sealed record ClearGuestSignatureCommand(Guid Id) : ICommand;
