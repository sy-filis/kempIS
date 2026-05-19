using SharedKernel;

namespace Domain.Reservations.Guests.DomainEvents;

public sealed record GuestCreatedDomainEvent(Guid GuestId) : IDomainEvent;
