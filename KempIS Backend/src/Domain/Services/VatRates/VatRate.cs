using SharedKernel;

namespace Domain.Services.VatRates;

public sealed class VatRate : Entity
{
  public Guid Id { get; set; }
  public string Name { get; set; }
  public decimal Rate { get; set; }
  public bool IsActive { get; set; }
}
