using SharedKernel;

namespace Domain.Services.Services;

public sealed class Service : Entity
{
  public Guid Id { get; set; }

  public ServiceGroup ServiceGroup { get; set; }

  public Guid ServiceTypeId { get; set; }

  public Guid VatRateId { get; set; }

  public string Name { get; set; }

  public decimal BasePrice { get; set; }

  public bool IsActive { get; set; }
}
