using SharedKernel;

namespace Domain.Reservations.Spots;

public sealed record SpotCreatedDomainEvent(Guid SpotId) : IDomainEvent;
