using SharedKernel;

namespace Domain.Finance.Payments;

public static class PaymentErrors
{
  public static Error NotFound(Guid paymentId) => Error.NotFound(
      "Payments.NotFound",
      $"The Payment with the Id = '{paymentId}' was not found");
}
