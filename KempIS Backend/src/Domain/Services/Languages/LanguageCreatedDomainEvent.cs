using SharedKernel;

namespace Domain.Services;

public sealed record LanguageCreatedDomainEvent(Guid LanguageId) : IDomainEvent;
