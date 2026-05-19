using Application.Finance.Bills.CreateRepairBill;
using Application.Finance.Bills.Shared;
using Domain.Finance.Payments;
using FluentValidation.TestHelper;

namespace Application.UnitTests.Finance.Bills;

public sealed class CreateRepairBillCommandValidatorTests
{
  private readonly CreateRepairBillCommandValidator _sut = new();

  private static CreateRepairBillCommand Cmd(string reason, uint recapSingle = 1u, uint recapDay = 1u) =>
    new(
      Guid.NewGuid(),
      PaymentType.Cash,
      reason,
      [new BillItemInput(Guid.NewGuid(), 1u, 100m, 21m, recapSingle, recapDay)]);

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void Reason_EmptyOrWhitespace_Fails(string reason) =>
    _sut.TestValidate(Cmd(reason)).ShouldHaveValidationErrorFor(c => c.Reason);

  [Fact]
  public void Reason_TooLong_Fails() =>
    _sut.TestValidate(Cmd(new string('x', 501))).ShouldHaveValidationErrorFor(c => c.Reason);

  [Fact]
  public void RecapSingleQuantityZero_Fails() =>
    _sut.TestValidate(Cmd("ok", recapSingle: 0u)).ShouldHaveValidationErrorFor("Items[0].RecapSingleQuantity");

  [Fact]
  public void RecapDayQuantityZero_Fails() =>
    _sut.TestValidate(Cmd("ok", recapDay: 0u)).ShouldHaveValidationErrorFor("Items[0].RecapDayQuantity");

  [Fact]
  public void HappyPath_Passes() =>
    _sut.TestValidate(Cmd("reasonable reason")).ShouldNotHaveAnyValidationErrors();
}
