using SharedKernel;

namespace Domain.Finance.Bills;

public sealed record BillCreatedDomainEvent(Guid BillId) : IDomainEvent;
