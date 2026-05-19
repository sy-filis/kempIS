using SharedKernel;

namespace Domain.Reservations.SpotGroups;

public sealed class SpotGroup : Entity
{
  public Guid Id { get; set; }

  public Guid ServiceId { get; set; }

  public string Name { get; set; }

  public string? Description { get; set; }

  public uint Capacity { get; set; }

  public bool IsActive { get; set; }

  public string ImageUrl { get; set; }

  public string DetailsUrl { get; set; }
}
