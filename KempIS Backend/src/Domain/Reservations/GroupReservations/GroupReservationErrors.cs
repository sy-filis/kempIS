using SharedKernel;

namespace Domain.Reservations;

public static class GroupReservationErrors
{
  public static Error NotFound(Guid groupReservationId) => Error.NotFound(
      "GroupReservation.NotFound",
      $"The GroupReservation with the Id = '{groupReservationId}' was not found");

  public static Error SecretInvalid => Error.Problem(
      "GroupReservation.SecretInvalid",
      "The provided secret does not match the group reservation.");

  public static Error Canceled(Guid groupReservationId) => Error.Problem(
      "GroupReservation.Canceled",
      $"The GroupReservation with the Id = '{groupReservationId}' is canceled.");

  public static Error AlreadyCanceled(Guid groupReservationId) => Error.Problem(
      "GroupReservation.AlreadyCanceled",
      $"The GroupReservation with the Id = '{groupReservationId}' is already canceled.");

  public static Error PeriodOutsideGroup(Guid groupReservationId) => Error.Problem(
      "GroupReservation.PeriodOutsideGroup",
      $"The requested period does not overlap with GroupReservation '{groupReservationId}' period.");

  public static Error SpotNotHeldByGroup(Guid spotId, Guid groupReservationId) => Error.Problem(
      "GroupReservation.SpotNotHeldByGroup",
      $"The spot '{spotId}' is not held by GroupReservation '{groupReservationId}'.");

  public static Error NoSpotsProvided => Error.Problem(
      "GroupReservation.NoSpotsProvided",
      "At least one spot must be provided when creating a group reservation.");
}
