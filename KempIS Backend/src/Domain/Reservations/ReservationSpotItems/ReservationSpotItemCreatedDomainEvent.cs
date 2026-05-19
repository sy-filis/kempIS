using SharedKernel;

namespace Domain.Reservations.ReservationSpotItems;

public sealed record ReservationSpotItemCreatedDomainEvent(Guid ReservationSpotItemId) : IDomainEvent;
