using SharedKernel;

namespace Domain.Operations.SpotGroupOOFItems;

public sealed record SpotGroupOofItemCreatedDomainEvent(Guid SpotGroupOOFItemId) : IDomainEvent;
