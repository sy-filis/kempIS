using SharedKernel;

namespace Domain.Operations.Events;

public sealed class EventSpotGroupItem : Entity
{
  public Guid Id { get; set; }

  public Guid EventId { get; set; }

  public Guid SpotGroupId { get; set; }
}
