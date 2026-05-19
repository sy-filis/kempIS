using System.Globalization;
using System.Security.Claims;
using Application.Abstractions.Authentication;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Web.Api.Endpoints.Auth;

internal sealed class RefreshEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("auth/refresh", async (
      RefreshRequest request,
      SignInManager<ApplicationUser> signInManager,
      IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
      TimeProvider timeProvider) =>
    {
      Microsoft.AspNetCore.Authentication.AuthenticationTicket? refreshTicket =
          bearerTokenOptions
            .Get(IdentityConstants.BearerScheme)
            .RefreshTokenProtector
            .Unprotect(request.RefreshToken);

      if (refreshTicket?.Properties?.ExpiresUtc is not { } expiresUtc ||
          timeProvider.GetUtcNow() >= expiresUtc ||
          await signInManager.ValidateSecurityStampAsync(refreshTicket.Principal) is not ApplicationUser user)
      {
        return Results.Challenge();
      }

      string? sessionExpiresAtRaw =
        refreshTicket.Principal?.FindFirstValue(AuthClaims.SessionExpiresAt);
      if (!DateTimeOffset.TryParse(
            sessionExpiresAtRaw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTimeOffset sessionExpiresAt) ||
          timeProvider.GetUtcNow() >= sessionExpiresAt)
      {
        return Results.Challenge();
      }

      ClaimsPrincipal newPrincipal = await signInManager.CreateUserPrincipalAsync(user);
      ((ClaimsIdentity)newPrincipal.Identity!).AddClaim(new Claim(
        AuthClaims.SessionExpiresAt,
        sessionExpiresAt.ToString("O", CultureInfo.InvariantCulture)));

      return Results.SignIn(newPrincipal, authenticationScheme: IdentityConstants.BearerScheme);
    })
    .WithTags(Tags.Auth)
    .WithName("AuthRefresh")
    .WithSummary("Refresh an expired bearer token")
    .WithDescription("""
      Exchanges a valid refresh token for a new bearer-token envelope without requiring the
      user to re-authenticate via passkey. The endpoint is anonymous because callers present
      only the refresh token; identity is established by unprotecting that token.

      **Behavior:** the refresh token is unprotected with the configured data protector and
      checked against (a) its embedded expiry, (b) the user's current security stamp, and
      (c) the absolute session deadline stamped at passkey login. The session deadline is
      copied verbatim into the newly issued refresh token so the cap is preserved across
      successive refreshes - once the deadline is reached, the user must re-authenticate.

      **Response body:** on success the body is the bearer token envelope produced by
      ASP.NET Identity's bearer-token handler:
      `{ tokenType, accessToken, expiresIn, refreshToken }`.

      **Errors:** `401` the refresh token is malformed, expired, lacks a session deadline
      claim, or the deadline has passed; the user's security stamp no longer matches; or any
      other condition that invalidates the refresh.
      """)
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status401Unauthorized)
    .AllowAnonymous();
  }
}

internal sealed record RefreshRequest(string RefreshToken);
