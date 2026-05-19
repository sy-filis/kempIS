using SharedKernel;

namespace Domain.Reservations.Spots;

public static class SpotErrors
{
  public static Error NotFound(Guid spotId) => Error.NotFound(
      "Spots.NotFound",
      $"The Spot with the Id = '{spotId}' was not found");
}
