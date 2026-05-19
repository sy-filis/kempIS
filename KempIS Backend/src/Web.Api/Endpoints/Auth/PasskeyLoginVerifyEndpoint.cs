using System.Globalization;
using System.Security.Claims;
using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Auth.Commands.PasskeyLoginVerify;
using Infrastructure.Authentication;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Auth;

internal sealed class PasskeyLoginVerifyEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("auth/passkeys/login/verify", async (
      PasskeyLoginVerifyRequest request,
      ICommandHandler<PasskeyLoginVerifyCommand, Guid> handler,
      UserManager<ApplicationUser> userManager,
      SignInManager<ApplicationUser> signInManager,
      BearerTokenSettings bearerTokenSettings,
      TimeProvider timeProvider,
      CancellationToken cancellationToken) =>
    {
      var command = new PasskeyLoginVerifyCommand(request.Credential);

      Result<Guid> result = await handler.Handle(command, cancellationToken);

      if (result.IsFailure)
      {
        return CustomResults.Problem(result);
      }

      ApplicationUser? user = await userManager.FindByIdAsync(result.Value.ToString());
      if (user is null)
      {
        return CustomResults.Problem(Result.Failure(AuthErrors.UserNotFound));
      }

      ClaimsPrincipal principal = await signInManager.CreateUserPrincipalAsync(user);

      DateTimeOffset sessionExpiresAt =
        timeProvider.GetUtcNow().AddMinutes(bearerTokenSettings.SessionAbsoluteExpirationMinutes);
      ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(
        AuthClaims.SessionExpiresAt,
        sessionExpiresAt.ToString("O", CultureInfo.InvariantCulture)));

      return Results.SignIn(principal, authenticationScheme: IdentityConstants.BearerScheme);
    })
    .WithTags(Tags.Auth)
    .WithName("PasskeyLoginVerify")
    .WithSummary("Complete a passkey login (returns a bearer token)")
    .WithDescription("""
      Verifies a WebAuthn assertion produced by the browser against the challenge issued by
      `auth/passkeys/login/challenge`, then signs the matching user in with the ASP.NET Identity
      bearer-token scheme. The endpoint is anonymous because the assertion itself authenticates
      the user.

      **Behavior:** the credential string must be the JSON returned from
      `navigator.credentials.get(...)`; an empty body is rejected by validation. A successful
      assertion updates the stored passkey's sign counter and last-used timestamp. The signed-in
      principal is stamped with an absolute session deadline (`SessionAbsoluteExpirationMinutes`,
      default 12 h); calls to `auth/refresh` past that deadline are rejected and force a
      re-login regardless of refresh-token activity.

      **Response body:** on success the body is the bearer token envelope produced by
      ASP.NET Identity's bearer-token handler:
      `{ tokenType, accessToken, expiresIn, refreshToken }`.

      **Errors:** `400` empty credential payload or invalid/forged assertion (signature
      mismatch, replay, or attestation rejected by the authenticator). `404` the assertion
      verified successfully but no user record exists for the resolved user id.
      """)
    .Produces(StatusCodes.Status200OK)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .AllowAnonymous();
  }
}

internal sealed record PasskeyLoginVerifyRequest(string Credential);
