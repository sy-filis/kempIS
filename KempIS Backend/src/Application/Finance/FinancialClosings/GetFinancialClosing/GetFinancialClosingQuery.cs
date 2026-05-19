using Application.Abstractions.Messaging;

namespace Application.Finance.FinancialClosings.GetFinancialClosing;

public sealed record GetFinancialClosingQuery(Guid Id)
  : IQuery<FinancialClosingDetailResponse>;
