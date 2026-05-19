using SharedKernel;

namespace Domain.Operations.MaintenanceIssues;

public static class MaintenanceIssueErrors
{
  public static Error NotFound(Guid id) => Error.NotFound(
      "MaintenanceIssues.NotFound",
      $"The MaintenanceIssue with the Id = '{id}' was not found");

  public static Error AlreadyResolved(Guid id) => Error.Conflict(
      "MaintenanceIssues.AlreadyResolved",
      $"The MaintenanceIssue with the Id = '{id}' has already been resolved.");
}
