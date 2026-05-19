using SharedKernel;

namespace Domain.Operations.OutOfOrders;

public sealed record OutOfOrderCreatedDomainEvent(Guid OutOfOrderId) : IDomainEvent;
