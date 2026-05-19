namespace Application.Finance.FinancialClosings.CreateFinancialClosing;

public sealed record CreateFinancialClosingResponse(
  Guid Id,
  uint FinancialClosingId,
  decimal TotalAmount,
  int BillCount);
