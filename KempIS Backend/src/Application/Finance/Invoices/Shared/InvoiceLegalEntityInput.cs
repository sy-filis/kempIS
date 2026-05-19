using Domain.Common;

namespace Application.Finance.Invoices.Shared;

public sealed record InvoiceLegalEntityInput(string Name, string Cin, string? Tin, Address Address);
