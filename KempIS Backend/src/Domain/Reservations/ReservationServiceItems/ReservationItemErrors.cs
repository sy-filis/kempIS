using SharedKernel;

namespace Domain.Reservations.ReservationServiceItems;

public static class ReservationItemErrors
{
  public static Error NotFound(Guid reservationItemId) => Error.NotFound(
      "ReservationServiceItems.NotFound",
      $"The ReservationItem with the Id = '{reservationItemId}' was not found");
}
