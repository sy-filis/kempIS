using Application.Auth.Commands.RegisterPasskeyChallenge;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Auth.Commands.RegisterPasskeyChallenge;

public sealed class RegisterPasskeyChallengeCommandValidatorTests
{
  private readonly RegisterPasskeyChallengeCommandValidator _validator = new();

  [Fact]
  public void ValidCommand_Passes()
      => _validator.TestValidate(new RegisterPasskeyChallengeCommand(Guid.NewGuid()))
          .ShouldNotHaveAnyValidationErrors();

  [Fact]
  public void UserId_WhenEmpty_Fails()
      => _validator.TestValidate(new RegisterPasskeyChallengeCommand(Guid.Empty))
          .ShouldHaveValidationErrorFor(c => c.UserId);
}
