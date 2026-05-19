using SharedKernel;

namespace Domain.Reservations;

public sealed record ReservationCreatedDomainEvent(Guid ReservationId) : IDomainEvent;
