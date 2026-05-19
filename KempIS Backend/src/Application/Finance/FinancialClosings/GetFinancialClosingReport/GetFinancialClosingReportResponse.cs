namespace Application.Finance.FinancialClosings.GetFinancialClosingReport;

public sealed record GetFinancialClosingReportResponse(byte[] Content, string ContentType, string FileName);
