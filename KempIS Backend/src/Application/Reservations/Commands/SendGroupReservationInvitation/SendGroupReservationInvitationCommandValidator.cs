using FluentValidation;

namespace Application.Reservations.Commands.SendGroupReservationInvitation;

internal sealed class SendGroupReservationInvitationCommandValidator
  : AbstractValidator<SendGroupReservationInvitationCommand>
{
  public SendGroupReservationInvitationCommandValidator()
  {
    RuleFor(c => c.GroupReservationId)
      .NotEmpty();

    RuleFor(c => c.Language)
      .NotEmpty()
      .MaximumLength(10);
  }
}
