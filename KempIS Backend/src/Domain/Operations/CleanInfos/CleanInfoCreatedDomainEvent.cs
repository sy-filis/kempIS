using SharedKernel;

namespace Domain.Operations.CleanInfos;

public sealed record CleanInfoCreatedDomainEvent(Guid CleanInfoId) : IDomainEvent;
