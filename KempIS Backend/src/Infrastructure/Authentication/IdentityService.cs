using System.Security.Cryptography;
using Application.Abstractions.Authentication;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Authentication;

internal sealed class IdentityService(UserManager<ApplicationUser> userManager)
  : IIdentityService
{
  public async Task<Result<Guid>> CreateUserAsync(
      string username,
      string name,
      string role,
      CancellationToken cancellationToken)
  {
    if (!Roles.All.Contains(role))
    {
      return Result.Failure<Guid>(AuthErrors.InvalidRole);
    }

    ApplicationUser? existing = await userManager.FindByNameAsync(username);
    if (existing is not null)
    {
      return Result.Failure<Guid>(AuthErrors.UsernameTaken);
    }

    var user = new ApplicationUser
    {
      Id = Guid.NewGuid(),
      UserName = username,
      Name = name
    };

    IdentityResult createResult = await userManager.CreateAsync(user);
    if (!createResult.Succeeded)
    {
      return Result.Failure<Guid>(
          AuthErrors.IdentityFailure(Describe(createResult.Errors)));
    }

    IdentityResult roleResult = await userManager.AddToRoleAsync(user, role);
    if (!roleResult.Succeeded)
    {
      return Result.Failure<Guid>(
          AuthErrors.IdentityFailure(Describe(roleResult.Errors)));
    }

    return user.Id;
  }

  public async Task<Result<IReadOnlyList<UserSummary>>> ListUsersAsync(
      bool includeDisabled,
      string? role,
      CancellationToken cancellationToken)
  {
    List<ApplicationUser> users = await userManager.Users.ToListAsync(cancellationToken);

    if (!string.IsNullOrEmpty(role))
    {
      IList<ApplicationUser> usersInRole = await userManager.GetUsersInRoleAsync(role);
      var roleIds = usersInRole.Select(u => u.Id).ToHashSet();
      users = users.Where(u => roleIds.Contains(u.Id)).ToList();
    }

    List<UserSummary> summaries = [];
    foreach (ApplicationUser user in users)
    {
      bool isDisabled = IsDisabled(user);
      if (!includeDisabled && isDisabled)
      {
        continue;
      }

      IList<string> roles = await userManager.GetRolesAsync(user);
      summaries.Add(new UserSummary(
          user.Id,
          user.UserName ?? string.Empty,
          user.Name,
          roles.ToList(),
          isDisabled,
          DateTime.MinValue));
    }

    return Result.Success<IReadOnlyList<UserSummary>>(summaries);
  }

  public async Task<Result<UserDetail>> GetUserAsync(
      Guid userId,
      CancellationToken cancellationToken)
  {
    ApplicationUser? user = await userManager.FindByIdAsync(userId.ToString());
    if (user is null)
    {
      return Result.Failure<UserDetail>(IdentityErrors.UserNotFound(userId));
    }

    IList<string> roles = await userManager.GetRolesAsync(user);
    IList<UserPasskeyInfo> passkeys = await userManager.GetPasskeysAsync(user);
    bool isDisabled = IsDisabled(user);

    return new UserDetail(
        user.Id,
        user.UserName ?? string.Empty,
        user.Name,
        roles.ToList(),
        isDisabled,
        DateTime.MinValue,
        passkeys.Count);
  }

  public async Task<Result> UpdateUserAsync(
      Guid userId,
      string username,
      string name,
      IReadOnlyList<string> roles,
      CancellationToken cancellationToken)
  {
    foreach (string role in roles)
    {
      if (!Roles.All.Contains(role))
      {
        return Result.Failure(IdentityErrors.RoleInvalid(role));
      }
    }

    ApplicationUser? user = await userManager.FindByIdAsync(userId.ToString());
    if (user is null)
    {
      return Result.Failure(IdentityErrors.UserNotFound(userId));
    }

    if (!string.Equals(user.UserName, username, StringComparison.Ordinal))
    {
      ApplicationUser? other = await userManager.FindByNameAsync(username);
      if (other is not null && other.Id != userId)
      {
        return Result.Failure(AuthErrors.UsernameTaken);
      }

      IdentityResult renameResult = await userManager.SetUserNameAsync(user, username);
      if (!renameResult.Succeeded)
      {
        return Result.Failure(AuthErrors.IdentityFailure(Describe(renameResult.Errors)));
      }
    }

    user.Name = name;

    IdentityResult updateResult = await userManager.UpdateAsync(user);
    if (!updateResult.Succeeded)
    {
      return Result.Failure(AuthErrors.IdentityFailure(Describe(updateResult.Errors)));
    }

    HashSet<string> targetRoles = [.. roles];
    HashSet<string> currentRoles = [.. await userManager.GetRolesAsync(user)];

    string[] toRemove = [.. currentRoles.Except(targetRoles)];
    if (toRemove.Length > 0)
    {
      IdentityResult removeResult = await userManager.RemoveFromRolesAsync(user, toRemove);
      if (!removeResult.Succeeded)
      {
        return Result.Failure(AuthErrors.IdentityFailure(Describe(removeResult.Errors)));
      }
    }

    string[] toAdd = [.. targetRoles.Except(currentRoles)];
    if (toAdd.Length > 0)
    {
      IdentityResult addResult = await userManager.AddToRolesAsync(user, toAdd);
      if (!addResult.Succeeded)
      {
        return Result.Failure(AuthErrors.IdentityFailure(Describe(addResult.Errors)));
      }
    }

    return Result.Success();
  }

  public async Task<Result> DisableUserAsync(
      Guid userId,
      CancellationToken cancellationToken)
  {
    ApplicationUser? user = await userManager.FindByIdAsync(userId.ToString());
    if (user is null)
    {
      return Result.Failure(IdentityErrors.UserNotFound(userId));
    }

    user.LockoutEnabled = true;
    user.LockoutEnd = DateTimeOffset.MaxValue;
    IdentityResult result = await userManager.UpdateAsync(user);
    return ToResult(result);
  }

  public async Task<Result<IReadOnlyList<PasskeySummary>>> ListPasskeysAsync(
      Guid userId,
      CancellationToken cancellationToken)
  {
    ApplicationUser? user = await userManager.FindByIdAsync(userId.ToString());
    if (user is null)
    {
      return Result.Failure<IReadOnlyList<PasskeySummary>>(IdentityErrors.UserNotFound(userId));
    }

    IList<UserPasskeyInfo> passkeys = await userManager.GetPasskeysAsync(user);

    var summaries = passkeys
        .Select(p => new PasskeySummary(
            CredentialIdToGuid(p.CredentialId),
            p.Name,
            p.CreatedAt.UtcDateTime))
        .ToList();

    return Result.Success<IReadOnlyList<PasskeySummary>>(summaries);
  }

  public async Task<Result> RevokePasskeyAsync(
      Guid userId,
      Guid passkeyId,
      CancellationToken cancellationToken)
  {
    ApplicationUser? user = await userManager.FindByIdAsync(userId.ToString());
    if (user is null)
    {
      return Result.Failure(IdentityErrors.UserNotFound(userId));
    }

    IList<UserPasskeyInfo> passkeys = await userManager.GetPasskeysAsync(user);

    UserPasskeyInfo? target = passkeys
        .FirstOrDefault(p => CredentialIdToGuid(p.CredentialId) == passkeyId);

    if (target is null)
    {
      return Result.Failure(IdentityErrors.PasskeyNotFound(passkeyId));
    }

    IdentityResult result = await userManager.RemovePasskeyAsync(user, target.CredentialId);
    return ToResult(result);
  }

  // SHA256 is used purely as a deterministic ID derivation, not for security.
  private static Guid CredentialIdToGuid(byte[] credentialId) =>
      new(SHA256.HashData(credentialId)[..16]);

  private static bool IsDisabled(ApplicationUser user) =>
      user.LockoutEnd.HasValue && user.LockoutEnd.Value >= DateTimeOffset.UtcNow;

  private static Result ToResult(IdentityResult result) =>
      result.Succeeded
          ? Result.Success()
          : Result.Failure(AuthErrors.IdentityFailure(string.Join("; ", result.Errors.Select(e => e.Description))));

  private static string Describe(IEnumerable<IdentityError> errors) =>
      string.Join("; ", errors.Select(e => e.Description));
}
