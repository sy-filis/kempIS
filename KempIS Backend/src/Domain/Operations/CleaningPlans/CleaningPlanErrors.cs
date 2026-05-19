using SharedKernel;

namespace Domain.Operations.CleaningPlans;

public static class CleaningPlanErrors
{
  public static Error SpotAlreadyInPlan(Guid spotId) => Error.Conflict(
      "CleaningPlans.SpotAlreadyInPlan",
      $"The Spot with the Id = '{spotId}' is already in this cleaning plan.");
}
