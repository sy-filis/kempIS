using Domain.Reservations.Spots;

namespace TestUtilities.Builders;

public sealed class SpotBuilder
{
  private Guid _id = Guid.NewGuid();
  private Guid _spotGroupId = Guid.NewGuid();
  private string _name = $"Spot-{Guid.NewGuid():N}";
  private string? _description;
  private bool _isActive = true;

  public SpotBuilder WithId(Guid id) { _id = id; return this; }
  public SpotBuilder InGroup(Guid spotGroupId) { _spotGroupId = spotGroupId; return this; }
  public SpotBuilder WithName(string name) { _name = name; return this; }
  public SpotBuilder WithDescription(string? description) { _description = description; return this; }
  public SpotBuilder Inactive() { _isActive = false; return this; }

  public Spot Build() => new()
  {
    Id = _id,
    SpotGroupId = _spotGroupId,
    Name = _name,
    Description = _description,
    IsActive = _isActive,
  };
}
