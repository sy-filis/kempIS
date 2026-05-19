using SharedKernel;

namespace Domain.Services.Languages;

public sealed class Language : Entity
{
  public Guid Id { get; set; }
  public string Code { get; set; }
  public string Name { get; set; }
}
