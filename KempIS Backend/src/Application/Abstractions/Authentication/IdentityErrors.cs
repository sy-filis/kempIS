using SharedKernel;

namespace Application.Abstractions.Authentication;

public static class IdentityErrors
{
  public static Error UserNotFound(Guid userId) => Error.NotFound(
      "Identity.UserNotFound",
      $"The user with the Id = '{userId}' was not found.");

  public static Error RoleInvalid(string role) => Error.Problem(
      "Identity.RoleInvalid",
      $"The role '{role}' is not a recognized role.");

  public static Error PasskeyNotFound(Guid passkeyId) => Error.NotFound(
      "Identity.PasskeyNotFound",
      $"The passkey with the Id = '{passkeyId}' was not found.");
}
