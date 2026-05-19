using SharedKernel;

namespace Domain.Operations.SpotOOFItems;

public static class SpotOofItemErrors
{
  public static Error NotFound(Guid spotOofItemId) => Error.NotFound(
      "SpotOofItems.NotFound",
      $"The SpotOofItem with the Id = '{spotOofItemId}' was not found");
}
