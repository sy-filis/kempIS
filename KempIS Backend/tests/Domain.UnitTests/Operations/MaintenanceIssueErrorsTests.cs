using Domain.Operations.MaintenanceIssues;
using SharedKernel;

namespace Domain.UnitTests.Operations;

public sealed class MaintenanceIssueErrorsTests
{
  [Fact]
  public void NotFound_CodeIsStable()
  {
    var id = Guid.NewGuid();

    Error error = MaintenanceIssueErrors.NotFound(id);

    error.Code.ShouldBe("MaintenanceIssues.NotFound");
    error.Description.ShouldContain(id.ToString());
  }

  [Fact]
  public void AlreadyResolved_CodeIsStable()
  {
    var id = Guid.NewGuid();

    Error error = MaintenanceIssueErrors.AlreadyResolved(id);

    error.Code.ShouldBe("MaintenanceIssues.AlreadyResolved");
    error.Description.ShouldContain(id.ToString());
  }
}
