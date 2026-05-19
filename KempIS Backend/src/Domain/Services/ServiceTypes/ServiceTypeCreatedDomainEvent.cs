using SharedKernel;

namespace Domain.Services.ServiceTypes;

public sealed record ServiceTypeCreatedDomainEvent(Guid ServiceTypeId) : IDomainEvent;
