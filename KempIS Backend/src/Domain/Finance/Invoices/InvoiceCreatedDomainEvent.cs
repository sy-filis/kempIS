using SharedKernel;

namespace Domain.Finance.Invoices;

public sealed record InvoiceCreatedDomainEvent(Guid InvoiceId) : IDomainEvent;
