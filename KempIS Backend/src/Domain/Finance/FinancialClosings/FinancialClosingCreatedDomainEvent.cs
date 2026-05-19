using SharedKernel;

namespace Domain.Finance.FinancialClosings;

public sealed record FinancialClosingCreatedDomainEvent(Guid FinancialClosingId) : IDomainEvent;
