using SharedKernel;

namespace Domain.Operations.MaintenanceIssues;

public sealed class MaintenanceIssue : Entity
{
  public Guid Id { get; set; }
  public Guid? SpotId { get; set; }
  public DateTime IssuedAtUtc { get; set; }
  public required string ProblemDescription { get; set; }
  public Guid? SolverUserId { get; set; }
  public DateTime? ResolvedAtUtc { get; set; }
  public string? Note { get; set; }
}
