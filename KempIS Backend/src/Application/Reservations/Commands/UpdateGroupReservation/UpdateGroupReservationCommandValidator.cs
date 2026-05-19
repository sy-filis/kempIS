using FluentValidation;

namespace Application.Reservations.Commands.UpdateGroupReservation;

internal sealed class UpdateGroupReservationCommandValidator : AbstractValidator<UpdateGroupReservationCommand>
{
  public UpdateGroupReservationCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();

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

    RuleFor(c => c.OrganizerName)
      .NotEmpty()
      .MaximumLength(256);

    RuleFor(c => c.OrganizerEmail)
      .NotEmpty()
      .EmailAddress()
      .MaximumLength(256);

    RuleFor(c => c.OrganizerPhone)
      .NotEmpty()
      .MaximumLength(50);

    RuleFor(c => c.Note)
      .MaximumLength(1000)
      .When(c => c.Note is not null);

    RuleFor(c => c.DisplayName)
      .MaximumLength(100)
      .When(c => c.DisplayName is not null);
  }
}
