using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Auth.Queries.GetCurrentUser;

internal sealed class GetCurrentUserQueryHandler(
  IUserContext userContext,
  IIdentityService identity,
  INoAuthState noAuthState)
  : IQueryHandler<GetCurrentUserQuery, CurrentUserResponse>
{
  // Matches NoAuthHandler.DevUserId.
  private static readonly Guid NoAuthUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

  public async Task<Result<CurrentUserResponse>> Handle(
    GetCurrentUserQuery query,
    CancellationToken cancellationToken)
  {
    if (noAuthState.IsEnabled)
    {
      return new CurrentUserResponse(
        NoAuthUserId,
        "no-auth",
        "Setup Operator",
        Roles.All,
        userContext.SessionExpiresAt);
    }

    Result<UserDetail> result = await identity.GetUserAsync(userContext.UserId, cancellationToken);

    return result.IsFailure
      ? Result.Failure<CurrentUserResponse>(result.Error)
      : new CurrentUserResponse(
          result.Value.Id,
          result.Value.Username,
          result.Value.Name,
          result.Value.Roles,
          userContext.SessionExpiresAt);
  }
}
