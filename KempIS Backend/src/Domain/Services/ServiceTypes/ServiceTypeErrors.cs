using SharedKernel;

namespace Domain.Services.ServiceTypes;

public static class ServiceTypeErrors
{
  public static Error NotFound(Guid serviceTypeId) => Error.NotFound(
      "ServiceTypes.NotFound",
      $"The ServiceType with the Id = '{serviceTypeId}' was not found");
}
