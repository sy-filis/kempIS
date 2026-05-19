using SharedKernel;

namespace Domain.Operations.CleaningPlans;

public sealed record CleaningPlanCreatedDomainEvent(Guid CleaningPlanId) : IDomainEvent;
