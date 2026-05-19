using Application.Auth.Commands.PasskeyLoginVerify;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Auth.Commands.PasskeyLoginVerify;

public sealed class PasskeyLoginVerifyCommandValidatorTests
{
  private readonly PasskeyLoginVerifyCommandValidator _validator = new();

  [Fact]
  public void Credential_WhenNonEmpty_Passes()
      => _validator.TestValidate(new PasskeyLoginVerifyCommand("credential-payload"))
          .ShouldNotHaveValidationErrorFor(c => c.Credential);

  [Fact]
  public void Credential_WhenEmpty_Fails()
      => _validator.TestValidate(new PasskeyLoginVerifyCommand(string.Empty))
          .ShouldHaveValidationErrorFor(c => c.Credential);
}
