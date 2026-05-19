using Application.Abstractions.Messaging;

namespace Application.Finance.FinancialClosings.GetFinancialClosingReport;

public sealed record GetFinancialClosingReportQuery(Guid FinancialClosingId)
  : IQuery<GetFinancialClosingReportResponse>;
