namespace Application.Finance.FinancialClosings.ListFinancialClosings;

public sealed record FinancialClosingSummary(
    Guid Id,
    uint FinancialClosingId,
    DateTime ClosedAtUtc,
    decimal TotalAmount,
    int BillCount,
    Guid? CreatedByUserId);
