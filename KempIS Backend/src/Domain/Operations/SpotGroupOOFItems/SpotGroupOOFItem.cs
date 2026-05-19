using SharedKernel;

namespace Domain.Operations.SpotGroupOOFItems;

public sealed class SpotGroupOofItem : Entity
{
  public Guid Id { get; set; }
  public Guid SpotGroupId { get; set; }
  public Guid OutOfOrderId { get; set; }
}
