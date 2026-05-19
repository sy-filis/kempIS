using SharedKernel;

namespace Domain.Reservations;

public sealed record ReservationConfirmedDomainEvent(Guid ReservationId) : IDomainEvent;
