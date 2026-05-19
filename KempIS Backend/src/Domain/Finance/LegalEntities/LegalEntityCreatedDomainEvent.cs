using SharedKernel;

namespace Domain.Finance.LegalEntities;

public sealed record LegalEntityCreatedDomainEvent(Guid LegalEntityId) : IDomainEvent;
