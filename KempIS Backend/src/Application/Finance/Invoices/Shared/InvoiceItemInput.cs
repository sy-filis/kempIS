namespace Application.Finance.Invoices.Shared;

public sealed record InvoiceItemInput(
  Guid ServiceGuid,
  decimal Quantity,
  decimal UnitPrice,
  decimal VatRatePercentage);
