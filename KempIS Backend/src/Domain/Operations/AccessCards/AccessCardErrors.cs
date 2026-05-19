using SharedKernel;

namespace Domain.Operations.AccessCards;

public static class AccessCardErrors
{
  public static Error NotFound(Guid accessCardId) => Error.NotFound(
      "AccessCards.NotFound",
      $"The AccessCard with the Id = '{accessCardId}' was not found");

  public static Error UidAlreadyInUse(ulong uid) => Error.Conflict(
      "AccessCards.UidAlreadyInUse",
      $"An access card with UID '{uid}' is already issued.");
}
