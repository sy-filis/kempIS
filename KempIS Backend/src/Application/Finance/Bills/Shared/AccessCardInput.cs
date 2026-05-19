namespace Application.Finance.Bills.Shared;

public sealed record AccessCardInput(
  ulong Uid,
  decimal Deposit,
  DateOnly ValidUntil,
  string? Note);
