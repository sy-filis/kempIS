using SharedKernel;

namespace Domain.Operations.SpotOOFItems;

public sealed class SpotOofItem : Entity
{
  public Guid Id { get; set; }
  public Guid SpotId { get; set; }
  public Guid OutOfOrderId { get; set; }
}
