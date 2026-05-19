using SharedKernel;
namespace Domain.Operations.CleanInfos;

public sealed class CleanInfo : Entity
{
  public Guid Id { get; set; }
  public Guid CleaningPlanId { get; set; }
  public Guid SpotId { get; set; }
  public Guid? ResponsibleUserId { get; set; }
  public DateTime? CompletedAtUtc { get; set; }
  public string? Note { get; set; }
}
