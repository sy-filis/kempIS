using SharedKernel;

namespace Domain.Finance.InvoiceItems;

public static class InvoiceItemErrors
{
  public static Error NotFound(Guid invoiceItemId) => Error.NotFound(
      "InvoiceItems.NotFound",
      $"The InvoiceItem with the Id = '{invoiceItemId}' was not found");
}
