using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Auth.Commands.RegisterPasskeyChallenge;
using Application.Auth.Commands.RegisterPasskeyVerify;
using Application.Identity.Users;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Identity;

internal sealed class UsersEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("users")
      .WithTags(Tags.Identity);

    group.MapPost(string.Empty, async (
      CreateUserRequest request,
      ICommandHandler<CreateUserCommand, Guid> handler,
      CancellationToken cancellationToken) =>
    {
      Result<Guid> result = await handler.Handle(
        new CreateUserCommand(request.Username, request.Name, request.Role), cancellationToken);

      return result.Match(
        id => Results.Created($"/users/{id}", new CreateUserResponse(id)),
        CustomResults.Problem);
    })
    .WithName("CreateUser")
    .WithSummary("Create a user")
    .WithDescription("""
      Provisions a new user in the ASP.NET Identity store and assigns them their initial role.
      The user has no passkeys until one is registered via the passkey-registration endpoints
      below; until then they cannot log in.

      **Behavior:** `username`, `name`, and `role` are required (validation rejects empty
      values and roles that are not in the supported role set). The username must be globally
      unique across the user store.

      **Errors:** `400` validation failure (empty fields, role outside the supported set) or
      the underlying ASP.NET Identity user creation / role assignment was rejected. `409`
      the supplied username is already in use by another user.
      """)
    .Produces<CreateUserResponse>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Manager);

    group.MapGet(string.Empty, async (
      bool? includeDisabled, string? role,
      IQueryHandler<ListUsersQuery, IReadOnlyList<UserSummary>> handler,
      CancellationToken cancellationToken) =>
    {
      ListUsersQuery q = new(includeDisabled ?? false, role);
      Result<IReadOnlyList<UserSummary>> result = await handler.Handle(q, cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("ListUsers")
    .WithSummary("List users")
    .WithDescription("""
      Returns the user directory with each user's id, username, display name, assigned roles,
      and disabled flag. By default disabled (locked-out) users are excluded; pass
      `includeDisabled=true` to include them. Pass `role=<roleName>` to restrict the result
      to users that hold a specific role.
      """)
    .Produces<IReadOnlyList<UserSummary>>(StatusCodes.Status200OK)
    .HasRole(Roles.Receptionist, Roles.CleaningStaff, Roles.Accountant, Roles.Manager);

    group.MapGet("{id:guid}", async (
      Guid id,
      IQueryHandler<GetUserQuery, UserDetail> handler,
      CancellationToken cancellationToken) =>
    {
      Result<UserDetail> result = await handler.Handle(new GetUserQuery(id), cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetUser")
    .WithSummary("Get a user by id")
    .WithDescription("""
      Returns the full detail view of a single user - id, username, display name, assigned
      roles, disabled flag, and the count of currently registered passkeys.

      **Errors:** `404` no user exists with the supplied id.
      """)
    .Produces<UserDetail>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);

    group.MapPut("{id:guid}", async (
      Guid id,
      UpdateUserRequest request,
      ICommandHandler<UpdateUserCommand> handler,
      CancellationToken cancellationToken) =>
    {
      Result result = await handler.Handle(
        new UpdateUserCommand(id, request.Username, request.Name, request.Roles), cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("UpdateUser")
    .WithSummary("Update a user's username, display name, and roles")
    .WithDescription("""
      Replaces the username, display name, and full role set on an existing user record.
      Lockout state is managed separately via the DELETE endpoint.

      **Behavior:** `username` and `name` are required and capped at 256 characters. `roles`
      must be a non-empty list of supported role names with no duplicates; the supplied list
      replaces the user's current role membership (additions and removals are reconciled
      automatically). If the supplied username is the same as the user's current one the
      rename step is skipped; otherwise the new username is checked for uniqueness against
      the user store.

      **Errors:** `400` validation failure (empty fields, fields too long, empty/duplicate/
      unknown roles) or the underlying ASP.NET Identity update was rejected. `404` no user
      exists with the supplied id. `409` the supplied username is already in use by another
      user.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Manager);

    group.MapDelete("{id:guid}", async (
      Guid id,
      ICommandHandler<DisableUserCommand> handler,
      CancellationToken cancellationToken) =>
    {
      Result result = await handler.Handle(new DisableUserCommand(id), cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("DisableUser")
    .WithSummary("Disable a user (soft delete)")
    .WithDescription("""
      Disables a user by enabling their lockout and setting their lockout end date to
      `DateTimeOffset.MaxValue`. The user row is preserved (soft delete) - re-enabling is
      possible by clearing the lockout out-of-band.

      **Side effects:** the next login attempt will be rejected by the ASP.NET Identity
      sign-in pipeline. Existing access tokens remain valid until their short expiry; refresh
      will only be denied once the security stamp is rotated separately.

      **Errors:** `400` the underlying ASP.NET Identity update failed. `404` no user exists
      with the supplied id.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);

    group.MapGet("{id:guid}/passkeys", async (
      Guid id,
      IQueryHandler<ListPasskeysQuery, IReadOnlyList<PasskeySummary>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<IReadOnlyList<PasskeySummary>> result = await handler.Handle(new ListPasskeysQuery(id), cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("ListUserPasskeys")
    .WithSummary("List a user's registered passkeys")
    .WithDescription("""
      Returns every passkey currently registered for the supplied user. Each entry exposes a
      stable id (derived deterministically from the WebAuthn credential id), the
      authenticator-supplied name, and the registration timestamp.

      **Errors:** `404` no user exists with the supplied id.
      """)
    .Produces<IReadOnlyList<PasskeySummary>>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);

    group.MapDelete("{id:guid}/passkeys/{passkeyId:guid}", async (
      Guid id, Guid passkeyId,
      ICommandHandler<RevokePasskeyCommand> handler,
      CancellationToken cancellationToken) =>
    {
      Result result = await handler.Handle(new RevokePasskeyCommand(id, passkeyId), cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("RevokeUserPasskey")
    .WithSummary("Revoke one of a user's passkeys")
    .WithDescription("""
      Removes the passkey identified by `passkeyId` from the supplied user. If the user has
      no other registered passkeys after revocation they will not be able to log in until a
      new passkey is registered.

      **Errors:** `400` the underlying ASP.NET Identity passkey-removal call failed. `404`
      no user exists with the supplied id, or the user holds no passkey with the supplied
      `passkeyId`.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);

    group.MapPost("{userId:guid}/passkeys/register/challenge", async (
      Guid userId,
      ICommandHandler<RegisterPasskeyChallengeCommand, string> handler,
      CancellationToken cancellationToken) =>
    {
      Result<string> result = await handler.Handle(
        new RegisterPasskeyChallengeCommand(userId), cancellationToken);

      return result.Match(
        json => Results.Content(json, "application/json"),
        CustomResults.Problem);
    })
    .WithName("RegisterUserPasskeyChallenge")
    .WithSummary("Begin passkey registration for a user")
    .WithDescription("""
      Returns a fresh WebAuthn creation options object scoped to the supplied user. The
      browser feeds this payload into `navigator.credentials.create(...)`; the resulting
      attestation is then submitted to the matching `verify` endpoint to complete
      registration.

      **Response body:** the body is a serialized WebAuthn
      `PublicKeyCredentialCreationOptions` JSON object produced by ASP.NET Identity's
      passkey support, ready to feed into the browser's WebAuthn API.

      **Errors:** `400` validation failure (empty user id). `404` no user exists with the
      supplied id.
      """)
    .Produces<string>(StatusCodes.Status200OK, contentType: "application/json")
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);

    group.MapPost("{userId:guid}/passkeys/register/verify", async (
      Guid userId,
      RegisterPasskeyVerifyRequest request,
      ICommandHandler<RegisterPasskeyVerifyCommand> handler,
      CancellationToken cancellationToken) =>
    {
      Result result = await handler.Handle(
        new RegisterPasskeyVerifyCommand(request.Credential, request.Name), cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("RegisterUserPasskeyVerify")
    .WithSummary("Complete passkey registration for a user")
    .WithDescription("""
      Verifies the WebAuthn attestation produced by the browser against the challenge issued
      by `users/{userId}/passkeys/register/challenge` and persists the new passkey on the
      user's record.

      **Behavior:** the credential string must be the JSON returned from
      `navigator.credentials.create(...)`; an empty body is rejected by validation. The
      `userId` route parameter is informational - the credential payload itself binds the
      attestation to a specific user via the embedded user entity id. The optional `name`
      field lets the caller assign a friendly label to the new passkey (e.g. "MacBook
      TouchID", "YubiKey 5C"); when omitted, the authenticator-supplied default is kept.

      **Errors:** `400` validation failure (empty credential, name longer than 100
      characters), the attestation was rejected (signature mismatch, replay, untrusted
      authenticator), or the underlying ASP.NET Identity passkey store update failed.
      `404` the embedded user entity id does not resolve to an existing user.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);
  }
}

internal sealed record CreateUserRequest(string Username, string Name, string Role);

internal sealed record CreateUserResponse(Guid Id);

internal sealed record UpdateUserRequest(
  string Username,
  string Name,
  IReadOnlyList<string> Roles);

internal sealed record RegisterPasskeyVerifyRequest(string Credential, string? Name = null);
