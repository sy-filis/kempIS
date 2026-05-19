using FluentValidation;

namespace Application.Reservations.Queries.Stats.GetGuestStatsByCountry;

internal sealed class GetGuestStatsByCountryQueryValidator
  : AbstractValidator<GetGuestStatsByCountryQuery>
{
  public GetGuestStatsByCountryQueryValidator()
  {
    RuleFor(q => q.From).NotEmpty();
    RuleFor(q => q.To)
      .NotEmpty()
      .GreaterThanOrEqualTo(q => q.From)
      .Must((q, to) => to.DayNumber - q.From.DayNumber <= 365)
        .WithMessage("Range must not exceed 366 days.");
  }
}
