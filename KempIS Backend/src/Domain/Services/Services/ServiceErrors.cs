using SharedKernel;

namespace Domain.Services.Services;

public static class ServiceErrors
{
  public static Error NotFound(Guid serviceId) => Error.NotFound(
      "Services.NotFound",
      $"The Service with the Id = '{serviceId}' was not found");
}
