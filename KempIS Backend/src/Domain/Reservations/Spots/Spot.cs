using Domain.Operations;
using SharedKernel;

namespace Domain.Reservations.Spots;

public sealed class Spot : Entity
{
  public Guid Id { get; set; }

  public required Guid SpotGroupId { get; set; }

  public required string Name { get; set; }

  public string? Description { get; set; }

  public required bool IsActive { get; set; }
}
