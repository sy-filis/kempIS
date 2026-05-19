using SharedKernel;

namespace Domain.Finance.Payers;

public sealed record PayerCreatedDomainEvent(Guid PayerId) : IDomainEvent;
