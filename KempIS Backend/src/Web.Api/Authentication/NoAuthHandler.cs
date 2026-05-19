using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Application.Abstractions.Authentication;
using Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Web.Api.Authentication;

internal sealed class NoAuthHandler(
  IOptionsMonitor<AuthenticationSchemeOptions> options,
  ILoggerFactory logger,
  UrlEncoder encoder,
  TimeProvider timeProvider)
  : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
  public const string SchemeName = "NoAuth";

  public static readonly Guid DevUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

  protected override Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    DateTimeOffset expires = timeProvider.GetUtcNow().AddHours(1);

    List<Claim> claims =
    [
      new(ClaimTypes.NameIdentifier, DevUserId.ToString()),
      new(ClaimTypes.Name, "no-auth@local"),
      new(AuthClaims.SessionExpiresAt, expires.ToString("O", CultureInfo.InvariantCulture)),
    ];
    claims.AddRange(Roles.All.Select(r => new Claim(ClaimTypes.Role, r)));

    var identity = new ClaimsIdentity(claims, SchemeName);
    var principal = new ClaimsPrincipal(identity);
    var ticket = new AuthenticationTicket(principal, SchemeName);
    return Task.FromResult(AuthenticateResult.Success(ticket));
  }
}
