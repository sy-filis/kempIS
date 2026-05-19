using SharedKernel;

namespace Domain.Reservations.Guests;

public static class GuestErrors
{
  public static Error NotFound(Guid guestId) => Error.NotFound(
      "Guest.NotFound",
      $"The Guest with the Id = '{guestId}' was not found");

  public static Error SignatureNotFound(Guid guestId) => Error.NotFound(
      "Guest.SignatureNotFound",
      $"No signature is stored for guest with Id = '{guestId}'.");
}

