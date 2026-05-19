using SharedKernel;

namespace Domain.Operations.CleanInfos;

public static class CleanInfoErrors
{
  public static Error NotFound(Guid cleanInfoId) => Error.NotFound(
      "CleanInfos.NotFound",
      $"The CleanInfo with the Id = '{cleanInfoId}' was not found");

  public static Error AlreadyCompleted(Guid id) => Error.Conflict(
      "CleanInfos.AlreadyCompleted",
      $"The CleanInfo with the Id = '{id}' has already been marked as cleaned.");
}
