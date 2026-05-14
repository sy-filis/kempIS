namespace KempISGatesService.Options;

public sealed class DatabaseOptions
{
  public const string SectionName = "Databases";

  public string DatabaseDirectory { get; init; } = string.Empty;

  public string DatabasePassword { get; init; } = string.Empty;

  public string UsersDatabaseFileName { get; init; } = "users.mdb";

  public string EventsDatabaseFileName { get; init; } = "events.mdb";

  public string EventOperator { get; init; } = "WEB_API";

  // Number of additional attempts after the first failure on a transient OleDbException. 0 disables retries.
  public int RetryCount { get; init; } = 2;
}
