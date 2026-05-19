using SharedKernel;

namespace Application.Abstractions.Authentication;

public static class AuthErrors
{
  public static readonly Error InvalidRole = Error.Problem(
      "Auth.InvalidRole",
      "The supplied role is not recognised.");

  public static readonly Error UsernameTaken = Error.Conflict(
      "Auth.UsernameTaken",
      "A user with this username already exists.");

  public static readonly Error UserNotFound = Error.NotFound(
      "Auth.UserNotFound",
      "User not found.");

  public static Error IdentityFailure(string description) =>
      Error.Problem("Auth.IdentityFailure", description);
}
