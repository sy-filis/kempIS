using SharedKernel;

namespace Domain.Finance.Invoices;

public sealed record InvoiceMarkedPaidDomainEvent(Guid InvoiceId) : IDomainEvent;
