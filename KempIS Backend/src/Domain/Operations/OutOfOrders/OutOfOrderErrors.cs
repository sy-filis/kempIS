using SharedKernel;

namespace Domain.Operations.OutOfOrders;

public static class OutOfOrderErrors
{
  public static Error NotFound(Guid outOfOrderId) => Error.NotFound(
      "OutOfOrders.NotFound",
      $"The OutOfOrder with the Id = '{outOfOrderId}' was not found");
}
