using Application.Abstractions.Messaging;

namespace Application.Reservations.Guests.Queries.GetGuestSignature;

public sealed record GetGuestSignatureQuery(Guid Id) : IQuery<GetGuestSignatureResponse>;
