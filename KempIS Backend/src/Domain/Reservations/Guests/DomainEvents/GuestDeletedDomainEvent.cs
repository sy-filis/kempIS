using SharedKernel;

namespace Domain.Reservations.Guests.DomainEvents;

public sealed record GuestDeletedDomainEvent(Guid GuestId) : IDomainEvent;
