using SharedKernel;

namespace Domain.Operations.Events;

public sealed record EventCreatedDomainEvent(Guid EventId) : IDomainEvent;
