using SharedKernel;

namespace Domain.Operations.SpotGroupOOFItems;

public static class SpotGroupOofItemErrors
{
  public static Error NotFound(Guid spotGroupOofItemId) => Error.NotFound(
      "SpotGroupOofItems.NotFound",
      $"The SpotGroupOofItem with the Id = '{spotGroupOofItemId}' was not found");
}
