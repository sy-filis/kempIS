using SharedKernel;

namespace Domain.Reservations.GroupReservations.DomainEvents;

public sealed record GroupReservationUpdatedDomainEvent(Guid GroupReservationId) : IDomainEvent;
