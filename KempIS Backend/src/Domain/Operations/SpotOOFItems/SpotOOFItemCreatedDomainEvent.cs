using SharedKernel;

namespace Domain.Operations.SpotOOFItems;

public sealed record SpotOofItemCreatedDomainEvent(Guid SpotOofItemId) : IDomainEvent;
