using Domain.Common;

namespace Domain.Finance.LegalEntities;

public sealed class LegalEntity
{
  public string Name { get; set; }
  public Address Address { get; set; }
  public string Cin { get; set; }
  public string? Tin { get; set; }
}
