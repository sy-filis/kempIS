using Domain.Reservations.SpotGroups;

namespace TestUtilities.Builders;

public sealed class SpotGroupBuilder
{
  private Guid _id = Guid.NewGuid();
  private Guid _serviceId = Guid.NewGuid();
  private string _name = "Default Group";
  private string? _description;
  private uint _capacity = 5;
  private bool _isActive = true;
  private string _imageUrl = "https://example.test/image.png";
  private string _detailsUrl = "https://example.test/details";

  public SpotGroupBuilder WithId(Guid id) { _id = id; return this; }
  public SpotGroupBuilder WithServiceId(Guid serviceId) { _serviceId = serviceId; return this; }
  public SpotGroupBuilder WithName(string name) { _name = name; return this; }
  public SpotGroupBuilder WithDescription(string? description) { _description = description; return this; }
  public SpotGroupBuilder WithCapacity(uint capacity) { _capacity = capacity; return this; }
  public SpotGroupBuilder WithImageUrl(string imageUrl) { _imageUrl = imageUrl; return this; }
  public SpotGroupBuilder WithDetailsUrl(string detailsUrl) { _detailsUrl = detailsUrl; return this; }
  public SpotGroupBuilder Inactive() { _isActive = false; return this; }

  public SpotGroup Build() => new()
  {
    Id = _id,
    ServiceId = _serviceId,
    Name = _name,
    Description = _description,
    Capacity = _capacity,
    IsActive = _isActive,
    ImageUrl = _imageUrl,
    DetailsUrl = _detailsUrl,
  };
}
