using SharedKernel;

namespace Domain.Finance.Invoices;

public sealed record InvoiceMarkedCreatedDomainEvent(Guid InvoiceId, string Number) : IDomainEvent;
