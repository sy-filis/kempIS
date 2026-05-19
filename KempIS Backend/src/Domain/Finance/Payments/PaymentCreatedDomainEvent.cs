using SharedKernel;

namespace Domain.Finance.Payments;

public sealed record PaymentCreatedDomainEvent(Guid PaymentId) : IDomainEvent;
