using SharedKernel;

namespace Domain.Reservations.GroupReservations.DomainEvents;

public sealed record GroupReservationDeletedDomainEvent(Guid GroupReservationId) : IDomainEvent;
