using SharedKernel;

namespace Domain.Finance.InvoiceItems;

public sealed record InvoiceItemCreatedDomainEvent(Guid InvoiceItemId) : IDomainEvent;
