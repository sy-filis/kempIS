using Domain.Common;

namespace Application.Finance.Bills.Shared;

public sealed record BillLegalEntityInput(string Name, string Cin, string? Tin, Address Address);
