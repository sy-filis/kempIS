using SharedKernel;

namespace Domain.Reservations.Guests.DomainEvents;

public sealed record GuestUpdatedDomainEvent(Guid GuestId) : IDomainEvent;
