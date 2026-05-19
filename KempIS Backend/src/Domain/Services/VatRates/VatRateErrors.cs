using SharedKernel;

namespace Domain.Services;

public static class VatRateErrors
{
  public static Error NotFound(Guid vatRateId) => Error.NotFound(
      "VatRate.NotFound",
      $"The VatRate with the Id = '{vatRateId}' was not found");
}

