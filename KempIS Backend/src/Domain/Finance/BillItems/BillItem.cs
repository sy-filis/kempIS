using Domain.Services;
using SharedKernel;

namespace Domain.Finance.BillItems;

public sealed class BillItem : Entity
{
  public Guid Id { get; set; }

  public Guid BillId { get; set; }

  public Guid? ServiceId { get; set; }

  public uint Quantity { get; set; }

  public decimal UnitPrice { get; set; }

  public decimal VatRatePercentage { get; set; }

  public uint RecapSingleQuantity { get; set; }

  public uint RecapDayQuantity { get; set; }
}
