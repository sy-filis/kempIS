using SharedKernel;

namespace Application.Reception.Realtime;

public static class RealtimeErrors
{
  public static readonly Error InvalidPairCode =
    Error.Problem("Reception.Realtime.InvalidPairCode", "The pair code is unknown or expired.");

  public static readonly Error RoleTaken =
    Error.Problem("Reception.Realtime.RoleTaken", "The other peer of this role has already joined this room.");

  public static readonly Error NotPaired =
    Error.Problem("Reception.Realtime.NotPaired", "The socket is not part of any active room.");

  public static readonly Error PayloadTooLarge =
    Error.Problem("Reception.Realtime.PayloadTooLarge", "The event payload exceeds the configured limit.");

  public static readonly Error BadRequest =
    Error.Problem("Reception.Realtime.BadRequest", "The event payload is malformed.");
}
