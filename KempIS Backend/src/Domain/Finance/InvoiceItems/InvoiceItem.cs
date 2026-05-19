using SharedKernel;

namespace Domain.Finance.InvoiceItems;

public sealed class InvoiceItem : Entity
{
  public Guid Id { get; set; }

  public Guid InvoiceId { get; set; }

  public Guid ServiceGuid { get; set; }

  public decimal Quantity { get; set; }

  public decimal UnitPrice { get; set; }

  public decimal VatRatePercentage { get; set; }
}
