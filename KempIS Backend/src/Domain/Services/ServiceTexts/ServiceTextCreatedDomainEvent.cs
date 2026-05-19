using SharedKernel;

namespace Domain.Services.ServiceTexts;

public sealed record ServiceTextCreatedDomainEvent(Guid ServiceTextId) : IDomainEvent;
