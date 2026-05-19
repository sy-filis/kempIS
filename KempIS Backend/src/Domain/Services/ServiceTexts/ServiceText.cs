using SharedKernel;

namespace Domain.Services.ServiceTexts;

public sealed class ServiceText : Entity
{
  public Guid Id { get; set; }
  public Guid ServiceId { get; set; }
  public Guid LanguageId { get; set; }
  public string PrintText { get; set; }
}
