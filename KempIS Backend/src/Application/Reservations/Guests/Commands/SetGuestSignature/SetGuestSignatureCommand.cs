using Application.Abstractions.Messaging;

namespace Application.Reservations.Guests.Commands.SetGuestSignature;

public sealed record SetGuestSignatureCommand(Guid Id, string SignaturePngBase64) : ICommand;
