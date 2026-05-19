using SharedKernel;

namespace Domain.Operations.CleaningPlans;

public sealed class CleaningPlan : Entity
{
  public Guid Id { get; set; }
  public DateOnly Date { get; set; }
  public DateTime? UpdatedAtUtc { get; set; }
  public Guid? UpdatedByUserId { get; set; }
}
