using SharedKernel;

namespace Domain.Finance.BillItems;

public static class BillItemErrors
{
  public static Error NotFound(Guid billItemId) => Error.NotFound(
      "BillItems.NotFound",
      $"The BillItem with the Id = '{billItemId}' was not found");
}
