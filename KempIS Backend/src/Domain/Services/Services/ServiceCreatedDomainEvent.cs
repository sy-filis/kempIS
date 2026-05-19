using SharedKernel;

namespace Domain.Services.Services;

public sealed record ServiceCreatedDomainEvent(Guid ServiceId) : IDomainEvent;
