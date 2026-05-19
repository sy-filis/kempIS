using FluentValidation;

namespace Application.Reservations.Queries.Stats.GetOccupancyStats;

internal sealed class GetOccupancyStatsQueryValidator
  : AbstractValidator<GetOccupancyStatsQuery>
{
  public GetOccupancyStatsQueryValidator()
  {
    RuleFor(q => q.From).NotEmpty();
    RuleFor(q => q.To)
      .NotEmpty()
      .GreaterThanOrEqualTo(q => q.From)
      .Must((q, to) => to.DayNumber - q.From.DayNumber <= 365)
        .WithMessage("Range must not exceed 366 days.");
  }
}
