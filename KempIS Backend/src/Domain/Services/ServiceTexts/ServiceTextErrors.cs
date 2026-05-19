using SharedKernel;

namespace Domain.Services.ServiceTexts;

public static class ServiceTextErrors
{
  public static Error NotFound(Guid serviceTextId) => Error.NotFound(
      "ServiceTexts.NotFound",
      $"The ServiceText with the Id = '{serviceTextId}' was not found");
}
