using SharedKernel;

namespace Application.Abstractions.EDoklady;

public static class EDokladyErrors
{
  public static Error NotFound(string id) => Error.NotFound(
      "EDoklady.NotFound",
      $"The eDoklady resource with the id = '{id}' was not found");

  public static Error BadRequest(string detail) => Error.Problem(
      "EDoklady.BadRequest",
      detail);

  public static readonly Error Unavailable = Error.Failure(
      "EDoklady.Unavailable",
      "eDoklady is currently unavailable");

  public static readonly Error Rejected = Error.Failure(
      "EDoklady.Rejected",
      "eDoklady rejected the request");
}
