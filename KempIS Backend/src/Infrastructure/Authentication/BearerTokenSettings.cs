namespace Infrastructure.Authentication;

public sealed class BearerTokenSettings
{
  public const string SectionName = "BearerToken";

  public int AccessTokenExpirationMinutes { get; set; } = 15;

  public int RefreshTokenExpirationMinutes { get; set; } = 12 * 60;

  // Hard cap measured from passkey verification; auth/refresh past this deadline is rejected.
  public int SessionAbsoluteExpirationMinutes { get; set; } = 12 * 60;
}
