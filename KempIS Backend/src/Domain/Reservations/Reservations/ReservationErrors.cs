using SharedKernel;

namespace Domain.Reservations;

public static class ReservationErrors
{
  public static Error NotFound(Guid reservationId) => Error.NotFound(
      "Reservation.NotFound",
      $"The Reservation with the Id = '{reservationId}' was not found");

  public static Error ReservationOverlapsWithExistingReservations => Error.Conflict(
      "Reservation.OverlapsWithExistingReservations",
      $"The reservation overlaps with existing reservations.");

  public static Error AlreadyCancelled(Guid reservationId) => Error.Problem(
      "Reservation.AlreadyCancelled",
      $"The Reservation with the Id = '{reservationId}' is already cancelled.");

  public static Error InvalidStateForCheckIn(Guid reservationId) => Error.Problem(
      "Reservation.InvalidStateForCheckIn",
      $"The Reservation with the Id = '{reservationId}' cannot be checked in from its current state.");

  public static Error SpotGroupNotFound(Guid spotGroupId) => Error.NotFound(
      "Reservation.SpotGroupNotFound",
      $"The SpotGroup with the Id = '{spotGroupId}' was not found.");

  public static Error SpotNotFound(Guid spotId) => Error.NotFound(
      "Reservation.SpotNotFound",
      $"The Spot with the Id = '{spotId}' was not found.");

  public static Error SpotGroupInactive(Guid spotGroupId) => Error.Problem(
      "Reservation.SpotGroupInactive",
      $"The SpotGroup with the Id = '{spotGroupId}' is not active.");

  public static Error RequestedQuantityExceedsCapacity(Guid spotGroupId) => Error.Problem(
      "Reservation.RequestedQuantityExceedsCapacity",
      $"The requested quantity for SpotGroup '{spotGroupId}' exceeds its available capacity in the requested period.");

  public static Error SpotOccupiedByReservation(Guid spotId) => Error.Conflict(
      "Reservation.SpotOccupiedByReservation",
      $"The spot '{spotId}' is occupied by another confirmed reservation in the requested period.");

  public static Error SpotOccupiedByOutOfOrder(Guid spotId) => Error.Conflict(
      "Reservation.SpotOccupiedByOutOfOrder",
      $"The spot '{spotId}' is out of order during the requested period.");

  public static Error SpotOccupiedByGroupReservation(Guid spotId) => Error.Conflict(
      "Reservation.SpotOccupiedByGroupReservation",
      $"The spot '{spotId}' is held by a group reservation in the requested period.");

  public static Error SecretInvalid => Error.Problem(
      "Reservation.SecretInvalid",
      "The provided secret does not match the reservation.");

  public static Error AlreadyOnlineCheckedIn(Guid id) => Error.Conflict(
      "Reservations.AlreadyOnlineCheckedIn",
      $"The Reservation with the Id = '{id}' has already completed online check-in.");

  public static Error MissingGuestSignatures(IReadOnlyCollection<Guid> guestIds) => Error.Problem(
      "Reservation.MissingGuestSignatures",
      $"The following guests must sign before check-in can complete: {string.Join(", ", guestIds)}.");

  public static Error CannotReturnKeysReservationNotCheckedIn(Guid reservationId) => Error.Problem(
      "Reservation.CannotReturnKeysReservationNotCheckedIn",
      $"Keys can only be returned for a reservation in CheckedIn state (Id = '{reservationId}').");

  public static Error NotEditableInState(Guid reservationId, ReservationStates.ReservationState state) => Error.Conflict(
      "Reservations.NotEditableInState",
      $"The Reservation with the Id = '{reservationId}' is in state '{state}' and cannot be edited.");

  public static Error ServiceNotFound(Guid serviceId) => Error.NotFound(
      "Reservations.ServiceNotFound",
      $"The Service with the Id = '{serviceId}' was not found.");

  public static Error VehicleNotOnReservation(Guid vehicleId) => Error.Problem(
      "Reservations.VehicleNotOnReservation",
      $"The Vehicle with the Id = '{vehicleId}' is not bound to this reservation.");

  public static Error SpotItemPaidCannotBeRemoved(Guid spotItemId) => Error.Conflict(
      "Reservation.SpotItemPaidCannotBeRemoved",
      $"The ReservationSpotItem with the Id = '{spotItemId}' is linked to a bill and cannot be removed.");

  public static Error VehiclePaidCannotBeRemoved(Guid vehicleId) => Error.Conflict(
      "Reservation.VehiclePaidCannotBeRemoved",
      $"The Vehicle with the Id = '{vehicleId}' is linked to a bill and cannot be removed.");

  public static Error InvalidLanguage(string language) => Error.Problem(
      "Reservations.InvalidLanguage",
      $"Language '{language}' is not supported. Supported values: {string.Join(", ", ReservationLanguages.All)}.");
}

