using Domain.Common;

namespace Application.Finance.Invoices.Shared;

public sealed record InvoicePayerInput(string Name, string Surname, Address Address);
