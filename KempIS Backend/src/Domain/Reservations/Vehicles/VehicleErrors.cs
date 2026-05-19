using SharedKernel;

namespace Domain.Reservations.Vehicles;

public static class VehicleErrors
{
  public static Error NotFound(Guid vehicleId) => Error.NotFound(
      "Vehicles.NotFound",
      $"The Vehicle with the Id = '{vehicleId}' was not found");
}
