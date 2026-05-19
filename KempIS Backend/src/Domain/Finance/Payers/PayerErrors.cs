using SharedKernel;

namespace Domain.Finance.Payers;

public static class PayerErrors
{
  public static Error NotFound(Guid payerId) => Error.NotFound(
      "Payers.NotFound",
      $"The Payer with the Id = '{payerId}' was not found");
}
