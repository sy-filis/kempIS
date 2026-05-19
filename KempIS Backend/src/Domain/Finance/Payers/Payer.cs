using Domain.Common;

namespace Domain.Finance.Payers;

public sealed class Payer
{
  public string Name { get; set; }
  public string Surname { get; set; }
  public Address Address { get; set; }
}
