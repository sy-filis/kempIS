using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Reads roles from <c>X-Test-Roles</c>; absent header yields NoResult so AllowAnonymous
/// and role-gated endpoints behave correctly.
/// </summary>
public sealed class TestAuthHandler(
  IOptionsMonitor<AuthenticationSchemeOptions> options,
  ILoggerFactory logger,
  UrlEncoder encoder)
  : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
  public const string SchemeName = "Test";
  public const string RolesHeader = "X-Test-Roles";
  public const string UserIdHeader = "X-Test-UserId";
  public const string UsernameHeader = "X-Test-Username";

  protected override Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    if (!Request.Headers.TryGetValue(RolesHeader, out Microsoft.Extensions.Primitives.StringValues rolesHeader))
    {
      return Task.FromResult(AuthenticateResult.NoResult());
    }

    string[] roles = rolesHeader.ToString()
      .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    string userId = Request.Headers.TryGetValue(UserIdHeader, out Microsoft.Extensions.Primitives.StringValues uid)
      ? uid.ToString()
      : Guid.NewGuid().ToString();
    string username = Request.Headers.TryGetValue(UsernameHeader, out Microsoft.Extensions.Primitives.StringValues un)
      ? un.ToString()
      : "test@example.com";

    List<Claim> claims =
    [
      new(ClaimTypes.NameIdentifier, userId),
      new(ClaimTypes.Name, username),
    ];
    claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

    var identity = new ClaimsIdentity(claims, SchemeName);
    var principal = new ClaimsPrincipal(identity);
    var ticket = new AuthenticationTicket(principal, SchemeName);
    return Task.FromResult(AuthenticateResult.Success(ticket));
  }
}
