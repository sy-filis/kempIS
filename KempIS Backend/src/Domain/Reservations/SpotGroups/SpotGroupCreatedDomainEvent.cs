using SharedKernel;

namespace Domain.Reservations.SpotGroups;

public sealed record SpotGroupCreatedDomainEvent(Guid SpotGroupId) : IDomainEvent;
