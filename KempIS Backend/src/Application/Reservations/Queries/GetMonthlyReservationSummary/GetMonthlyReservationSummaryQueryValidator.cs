using FluentValidation;

namespace Application.Reservations.Queries.GetMonthlyReservationSummary;

internal sealed class GetMonthlyReservationSummaryQueryValidator
  : AbstractValidator<GetMonthlyReservationSummaryQuery>
{
  public GetMonthlyReservationSummaryQueryValidator()
  {
    RuleFor(q => q.Year)
      .InclusiveBetween(2000, 2100);
  }
}
