using SharedKernel;

namespace Domain.Reservations.ReservationMakers;

public static class ReservationMakerErrors
{
  public static Error NotFound(Guid reservationMakerId) => Error.NotFound(
      "ReservationMaker.NotFound",
      $"The ReservationMaker with the Id = '{reservationMakerId}' was not found");
}
