using SharedKernel;

namespace Domain.Services.ServiceTypes;

public sealed class ServiceType : Entity
{
  public Guid Id { get; set; }
  public string Name { get; set; }
  public bool IsActive { get; set; }
}
