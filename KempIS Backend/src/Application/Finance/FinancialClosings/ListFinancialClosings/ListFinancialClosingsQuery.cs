using Application.Abstractions.Messaging;

namespace Application.Finance.FinancialClosings.ListFinancialClosings;

public sealed record ListFinancialClosingsQuery(DateOnly? From, DateOnly? To)
  : IQuery<IReadOnlyList<FinancialClosingSummary>>;
