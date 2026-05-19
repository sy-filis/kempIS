using Application.Auth.Commands.RegisterPasskeyVerify;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Auth.Commands.RegisterPasskeyVerify;

public sealed class RegisterPasskeyVerifyCommandValidatorTests
{
  private readonly RegisterPasskeyVerifyCommandValidator _validator = new();

  [Fact]
  public void Credential_WhenNonEmpty_Passes()
      => _validator.TestValidate(new RegisterPasskeyVerifyCommand("cred"))
          .ShouldNotHaveValidationErrorFor(c => c.Credential);

  [Fact]
  public void Credential_WhenEmpty_Fails()
      => _validator.TestValidate(new RegisterPasskeyVerifyCommand(string.Empty))
          .ShouldHaveValidationErrorFor(c => c.Credential);
}
