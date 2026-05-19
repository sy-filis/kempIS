using Domain.Reservations;
using FluentValidation;
using SharedKernel;

namespace Application.Reservations.Commands.CreateWebReservation;

internal sealed class CreateWebReservationCommandValidator : AbstractValidator<CreateWebReservationCommand>
{
  public CreateWebReservationCommandValidator(IDateTimeProvider dateTimeProvider)
  {
    RuleFor(c => c.Name)
      .NotEmpty()
      .MaximumLength(255);

    RuleFor(c => c.Surname)
      .NotEmpty()
      .MaximumLength(255);

    RuleFor(c => c.Email)
      .NotEmpty()
      .EmailAddress()
      .MaximumLength(255);

    RuleFor(c => c.Phone)
      .NotEmpty()
      .MaximumLength(50);

    RuleFor(c => c.From)
      .GreaterThanOrEqualTo(DateOnly.FromDateTime(dateTimeProvider.UtcNow))
      .WithMessage("Can not create reservation in past.")
      .NotEmpty();

    RuleFor(c => c.To)
      .NotEmpty()
      .GreaterThan(c => c.From)
      .WithMessage("'To' date must be after the 'From' date.");

    RuleFor(c => c.RequestedSpots)
      .NotEmpty()
      .WithMessage("At least one spot group must be requested.");

    RuleForEach(c => c.RequestedSpots).ChildRules(r =>
    {
      r.RuleFor(s => s.SpotGroupId).NotEmpty();
      r.RuleFor(s => s.Quantity).GreaterThan(0u);
    });

    RuleFor(c => c.Note)
      .MaximumLength(1000)
      .When(c => c.Note is not null);

    RuleFor(c => c.Language)
      .Must(language => language is null || ReservationLanguages.All.Contains(language))
      .WithMessage($"Language must be one of: {string.Join(", ", ReservationLanguages.All)}.");
  }
}
