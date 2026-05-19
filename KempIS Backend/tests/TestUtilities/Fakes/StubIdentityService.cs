using Application.Abstractions.Authentication;
using SharedKernel;

namespace TestUtilities.Fakes;

public sealed class StubIdentityService : IIdentityService
{
  public Result<Guid>? NextResult { get; set; }
  public string? LastUsername { get; private set; }
  public string? LastName { get; private set; }
  public string? LastRole { get; private set; }
  public IReadOnlyList<string>? LastRoles { get; private set; }
  public int CallCount { get; private set; }

  public Result<IReadOnlyList<UserSummary>>? ListUsersAsyncResult { get; set; }
  public Result<UserDetail>? GetUserAsyncResult { get; set; }
  public Result? UpdateUserAsyncResult { get; set; }
  public Result? DisableUserAsyncResult { get; set; }
  public Result<IReadOnlyList<PasskeySummary>>? ListPasskeysAsyncResult { get; set; }
  public Result? RevokePasskeyAsyncResult { get; set; }

  public void Reset()
  {
    NextResult = null;
    LastUsername = null;
    LastName = null;
    LastRole = null;
    LastRoles = null;
    CallCount = 0;
    ListUsersAsyncResult = null;
    GetUserAsyncResult = null;
    UpdateUserAsyncResult = null;
    DisableUserAsyncResult = null;
    ListPasskeysAsyncResult = null;
    RevokePasskeyAsyncResult = null;
  }

  public Task<Result<Guid>> CreateUserAsync(
      string username,
      string name,
      string role,
      CancellationToken cancellationToken)
  {
    LastUsername = username;
    LastName = name;
    LastRole = role;
    CallCount++;
    return Task.FromResult(NextResult ?? Result.Success(Guid.NewGuid()));
  }

  public Task<Result<IReadOnlyList<UserSummary>>> ListUsersAsync(
      bool includeDisabled,
      string? role,
      CancellationToken cancellationToken) =>
      Task.FromResult(ListUsersAsyncResult ?? Result.Success<IReadOnlyList<UserSummary>>([]));

  public Task<Result<UserDetail>> GetUserAsync(
      Guid userId,
      CancellationToken cancellationToken) =>
      Task.FromResult(GetUserAsyncResult ?? Result.Failure<UserDetail>(IdentityErrors.UserNotFound(userId)));

  public Task<Result> UpdateUserAsync(
      Guid userId,
      string username,
      string name,
      IReadOnlyList<string> roles,
      CancellationToken cancellationToken)
  {
    LastUsername = username;
    LastName = name;
    LastRoles = roles;
    return Task.FromResult(UpdateUserAsyncResult ?? Result.Success());
  }

  public Task<Result> DisableUserAsync(
      Guid userId,
      CancellationToken cancellationToken) =>
      Task.FromResult(DisableUserAsyncResult ?? Result.Failure(IdentityErrors.UserNotFound(userId)));

  public Task<Result<IReadOnlyList<PasskeySummary>>> ListPasskeysAsync(
      Guid userId,
      CancellationToken cancellationToken) =>
      Task.FromResult(ListPasskeysAsyncResult ?? Result.Failure<IReadOnlyList<PasskeySummary>>(IdentityErrors.UserNotFound(userId)));

  public Task<Result> RevokePasskeyAsync(
      Guid userId,
      Guid passkeyId,
      CancellationToken cancellationToken) =>
      Task.FromResult(RevokePasskeyAsyncResult ?? Result.Failure(IdentityErrors.UserNotFound(userId)));
}
