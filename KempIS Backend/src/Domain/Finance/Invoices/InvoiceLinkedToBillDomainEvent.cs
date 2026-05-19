using SharedKernel;

namespace Domain.Finance.Invoices;

public sealed record InvoiceLinkedToBillDomainEvent(Guid InvoiceId, Guid BillId) : IDomainEvent;
