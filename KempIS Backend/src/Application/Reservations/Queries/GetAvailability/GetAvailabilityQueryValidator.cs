using FluentValidation;

namespace Application.Reservations.Queries.GetAvailability;

internal sealed class GetAvailabilityQueryValidator : AbstractValidator<GetAvailabilityQuery>
{
  public GetAvailabilityQueryValidator()
  {
    RuleFor(q => q.From)
      .NotEmpty();

    RuleFor(q => q.To)
      .NotEmpty()
      .GreaterThanOrEqualTo(q => q.From)
      .WithMessage("'To' date must be on or after the 'From' date.");
  }
}
