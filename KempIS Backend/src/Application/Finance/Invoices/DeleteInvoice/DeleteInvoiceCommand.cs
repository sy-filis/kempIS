using Application.Abstractions.Messaging;

namespace Application.Finance.Invoices.DeleteInvoice;

public sealed record DeleteInvoiceCommand(Guid InvoiceId) : ICommand;
