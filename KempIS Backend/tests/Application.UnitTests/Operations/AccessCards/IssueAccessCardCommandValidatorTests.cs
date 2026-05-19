using Application.Operations.AccessCards;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Operations.AccessCards;

public sealed class IssueAccessCardCommandValidatorTests
{
  private readonly IssueAccessCardCommandValidator _sut = new();

  [Fact]
  public void Validator_Accepts_ValidCommand()
  {
    var cmd = new IssueAccessCardCommand(
      BillId: null, Uid: 1UL, Deposit: 0m, ValidUntil: new DateOnly(2026, 8, 15), Note: null);

    _sut.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
  }

  [Fact]
  public void Validator_Rejects_DefaultValidUntil()
  {
    var cmd = new IssueAccessCardCommand(
      BillId: null, Uid: 1UL, Deposit: 0m, ValidUntil: default, Note: null);

    _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.ValidUntil);
  }

  [Fact]
  public void Validator_Rejects_ZeroUid()
  {
    var cmd = new IssueAccessCardCommand(
      BillId: null, Uid: 0UL, Deposit: 0m, ValidUntil: new DateOnly(2026, 8, 15), Note: null);

    _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.Uid);
  }
}
