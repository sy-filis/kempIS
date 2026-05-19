using FluentValidation;

namespace Application.Auth.Commands.RegisterPasskeyVerify;

internal sealed class RegisterPasskeyVerifyCommandValidator
  : AbstractValidator<RegisterPasskeyVerifyCommand>
{
  public RegisterPasskeyVerifyCommandValidator()
  {
    RuleFor(c => c.Credential).NotEmpty();

    RuleFor(c => c.Name)
      .MaximumLength(100)
      .When(c => c.Name is not null);
  }
}
