using Domain.Operations.AccessCards;
using SharedKernel;
using Shouldly;

namespace Domain.UnitTests.Operations;

public sealed class AccessCardErrorsTests
{
  [Fact]
  public void UidAlreadyInUse_ReturnsConflictErrorWithExpectedCodeAndMessage()
  {
    const ulong uid = 42UL;

    Error error = AccessCardErrors.UidAlreadyInUse(uid);

    error.Type.ShouldBe(ErrorType.Conflict);
    error.Code.ShouldBe("AccessCards.UidAlreadyInUse");
    error.Description.ShouldContain("42");
  }
}
