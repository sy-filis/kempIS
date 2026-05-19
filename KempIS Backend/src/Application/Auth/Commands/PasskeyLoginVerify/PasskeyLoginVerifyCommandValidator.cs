using FluentValidation;

namespace Application.Auth.Commands.PasskeyLoginVerify;

internal sealed class PasskeyLoginVerifyCommandValidator
  : AbstractValidator<PasskeyLoginVerifyCommand>
{
  public PasskeyLoginVerifyCommandValidator()
  {
    RuleFor(c => c.Credential).NotEmpty();
  }
}
