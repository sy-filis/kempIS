using System.Globalization;
using System.Security.Claims;
using Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Authentication;

internal sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
  public Guid UserId =>
        httpContextAccessor
            .HttpContext?
            .User
            .GetUserId() ??
        throw new UserContextUnavailableException();

  public DateTimeOffset? SessionExpiresAt
  {
    get
    {
      string? raw = httpContextAccessor.HttpContext?.User.FindFirstValue(AuthClaims.SessionExpiresAt);
      return DateTimeOffset.TryParse(
          raw,
          CultureInfo.InvariantCulture,
          DateTimeStyles.RoundtripKind,
          out DateTimeOffset parsed)
          ? parsed
          : null;
    }
  }
}
