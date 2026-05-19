using SharedKernel;

namespace Domain.Reservations.ReservationSpotItems;

public static class ReservationSpotItemErrors
{
  public static Error NotFound(Guid reservationSpotItemId) => Error.NotFound(
      "ReservationSpotItems.NotFound",
      $"The ReservationSpotItem with the Id = '{reservationSpotItemId}' was not found");

  public static readonly Error CannotGiveKeyReservationNotConfirmedOrCheckedIn =
    Error.Problem(
      "ReservationSpotItems.CannotGiveKeyReservationNotConfirmedOrCheckedIn",
      "Keys can only be handed over while the parent reservation is Confirmed or CheckedIn.");
}
