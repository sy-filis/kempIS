using Domain.Common;

namespace Application.Finance.Bills.Shared;

public sealed record BillPayerInput(string Name, string Surname, Address Address);
