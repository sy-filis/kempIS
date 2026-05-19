using SharedKernel;

namespace Domain.Reservations.GroupReservations.DomainEvents;

public sealed record GroupReservationCreatedDomainEvent(Guid GroupReservationId) : IDomainEvent;
