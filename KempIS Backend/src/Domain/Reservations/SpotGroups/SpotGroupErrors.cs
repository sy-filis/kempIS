using SharedKernel;

namespace Domain.Reservations.SpotGroups;

public static class SpotGroupErrors
{
  public static Error NotFound(Guid spotGroupId) => Error.NotFound(
      "SpotGroups.NotFound",
      $"The SpotGroup with the Id = '{spotGroupId}' was not found");

  public static Error ServiceNotInSpotsGroup(Guid serviceId) => Error.Problem(
      "SpotGroup.ServiceNotInSpotsGroup",
      $"The Service with the Id = '{serviceId}' is not in the Spots group");
}
