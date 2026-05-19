using SharedKernel;

namespace Domain.Reservations.ReservationServiceItems;

public sealed record ReservationItemCreatedDomainEvent(Guid ReservationItemId) : IDomainEvent;
