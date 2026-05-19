namespace Application.Finance.Bills.Shared;

public sealed record BillItemInput(
  Guid? ServiceId,
  uint Quantity,
  decimal UnitPrice,
  decimal VatRatePercentage,
  uint RecapSingleQuantity,
  uint RecapDayQuantity);
