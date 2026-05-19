using Domain.Reservations;
using FluentValidation;

namespace Application.Reservations.Commands.CreateReservation;

internal sealed class CreateReservationCommandValidator : AbstractValidator<CreateReservationCommand>
{
  public CreateReservationCommandValidator()
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
      .NotEmpty();

    RuleFor(c => c.To)
      .NotEmpty()
      .GreaterThan(c => c.From)
      .WithMessage("'To' date must be after the 'From' date.");

    RuleFor(c => c.SpotIds)
      .NotEmpty()
      .WithMessage("At least one spot must be specified.");

    RuleForEach(c => c.SpotIds)
      .NotEmpty();

    RuleFor(c => c.Note)
      .MaximumLength(1000)
      .When(c => c.Note is not null);

    RuleFor(c => c.DisplayName)
      .MaximumLength(100)
      .When(c => c.DisplayName is not null);

    RuleFor(c => c.Services)
      .Must(items => items is null || items.Select(i => i.ServiceId).Distinct().Count() == items.Count)
        .WithMessage("ServiceIds must be unique.");

    RuleForEach(c => c.Services).ChildRules(line =>
    {
      line.RuleFor(l => l.ServiceId).NotEmpty();
    });

    RuleForEach(c => c.Vehicles).ChildRules(line =>
    {
      line.RuleFor(l => l.RegistrationNumber)
        .NotEmpty()
        .MaximumLength(20);
    });

    RuleFor(c => c.Language)
      .Must(language => language is null || ReservationLanguages.All.Contains(language))
      .WithMessage($"Language must be one of: {string.Join(", ", ReservationLanguages.All)}.");
  }
}
