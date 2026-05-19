using Application.Abstractions.Messaging;

namespace Application.Reservations.Queries.GetMonthlyReservationSummary;

public sealed record GetMonthlyReservationSummaryQuery(int Year)
  : IQuery<MonthlyReservationSummaryResponse>;

public sealed record MonthlyReservationSummaryResponse(
  int Year,
  IReadOnlyList<int> Months);
