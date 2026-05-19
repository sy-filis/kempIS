using SharedKernel;

namespace Domain.Finance.FinancialClosings;

public static class FinancialClosingErrors
{
  public static Error NotFound(Guid financialClosingId) => Error.NotFound(
      "FinancialClosings.NotFound",
      $"The FinancialClosing with the Id = '{financialClosingId}' was not found");

  public static Error NoOpenBills() => Error.Conflict(
      "FinancialClosings.NoOpenBills",
      "There are no bills eligible for closing. All existing bills are already part of a financial closing.");
}
