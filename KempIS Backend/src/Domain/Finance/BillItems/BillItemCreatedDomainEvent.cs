using SharedKernel;

namespace Domain.Finance.BillItems;

public sealed record BillItemCreatedDomainEvent(Guid BillItemId) : IDomainEvent;
