using Application.Operations.AccessCards;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Operations.AccessCards;

public sealed class UpdateAccessCardCommandValidatorTests
{
  private readonly UpdateAccessCardCommandValidator _sut = new();

  [Fact]
  public void Validator_Accepts_ValidCommand()
  {
    var cmd = new UpdateAccessCardCommand(
      Id: Guid.NewGuid(), ValidUntil: new DateOnly(2026, 8, 15), Note: "extra key");

    _sut.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
  }

  [Fact]
  public void Validator_Rejects_EmptyId()
  {
    var cmd = new UpdateAccessCardCommand(
      Id: Guid.Empty, ValidUntil: new DateOnly(2026, 8, 15), Note: null);

    _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.Id);
  }

  [Fact]
  public void Validator_Rejects_DefaultValidUntil()
  {
    var cmd = new UpdateAccessCardCommand(
      Id: Guid.NewGuid(), ValidUntil: default, Note: null);

    _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.ValidUntil);
  }
}
