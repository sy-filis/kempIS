using SharedKernel;

namespace Domain.Services;

public sealed record VatRateCreatedDomainEvent(Guid VatRateId) : IDomainEvent;
