namespace Application.Abstractions.Authentication;

public static class AuthClaims
{
  /// <summary>ISO 8601 round-trip string; stamped at passkey login and copied forward by refresh.</summary>
  public const string SessionExpiresAt = "session_expires_at";
}
