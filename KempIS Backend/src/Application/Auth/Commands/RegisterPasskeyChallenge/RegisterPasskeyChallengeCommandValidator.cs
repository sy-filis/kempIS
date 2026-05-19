using FluentValidation;

namespace Application.Auth.Commands.RegisterPasskeyChallenge;

internal sealed class RegisterPasskeyChallengeCommandValidator
  : AbstractValidator<RegisterPasskeyChallengeCommand>
{
  public RegisterPasskeyChallengeCommandValidator()
  {
    RuleFor(c => c.UserId).NotEmpty();
  }
}
