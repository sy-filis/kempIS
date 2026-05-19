using SharedKernel;

namespace Application.Abstractions.Authentication;

public interface IIdentityService
{
  Task<Result<Guid>> CreateUserAsync(
      string username,
      string name,
      string role,
      CancellationToken cancellationToken);

  Task<Result<IReadOnlyList<UserSummary>>> ListUsersAsync(
      bool includeDisabled,
      string? role,
      CancellationToken cancellationToken);

  Task<Result<UserDetail>> GetUserAsync(
      Guid userId,
      CancellationToken cancellationToken);

  Task<Result> UpdateUserAsync(
      Guid userId,
      string username,
      string name,
      IReadOnlyList<string> roles,
      CancellationToken cancellationToken);

  Task<Result> DisableUserAsync(
      Guid userId,
      CancellationToken cancellationToken);

  Task<Result<IReadOnlyList<PasskeySummary>>> ListPasskeysAsync(
      Guid userId,
      CancellationToken cancellationToken);

  Task<Result> RevokePasskeyAsync(
      Guid userId,
      Guid passkeyId,
      CancellationToken cancellationToken);
}
