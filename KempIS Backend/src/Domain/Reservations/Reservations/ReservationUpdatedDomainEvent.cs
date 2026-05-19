using SharedKernel;

namespace Domain.Reservations;

public sealed record ReservationUpdatedDomainEvent(Guid ReservationId) : IDomainEvent;
