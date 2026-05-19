using SharedKernel;

namespace Domain.Operations.Events;

public sealed class Event : Entity
{
  public Guid Id { get; set; }
  public string Name { get; set; }
  public string? Description { get; set; }
  public DateOnly StartsAt { get; set; }
  public DateOnly? EndsAt { get; set; }
  public List<EventSpotGroupItem> SpotGroupItems { get; set; } = [];
}
