using System.Data.OleDb;
using System.Diagnostics;

namespace KempISGatesService.Infrastructure;

// Retries an OleDb operation on OleDbException up to retryCount additional times. The operation
// itself is always sync — OleDb has no async API — but the async overloads release the calling
// thread during the inter-attempt delay so a retry storm cannot pin Kestrel workers.
internal static class OleDbRetry
{
  private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);

  public static T Execute<T>(Func<T> operation, int retryCount, ILogger logger, string operationName)
  {
    for (int attempt = 0; attempt <= retryCount; attempt++)
    {
      try
      {
        return operation();
      }
      catch (OleDbException ex) when (attempt < retryCount)
      {
        LogRetry(ex, logger, operationName, attempt, retryCount);
        Thread.Sleep(RetryDelay);
      }
    }

    throw new UnreachableException("OleDbRetry loop exited without returning or throwing.");
  }

  public static void Execute(Action operation, int retryCount, ILogger logger, string operationName) =>
    Execute(() => { operation(); return true; }, retryCount, logger, operationName);

  public static async Task<T> ExecuteAsync<T>(Func<T> operation, int retryCount, ILogger logger, string operationName)
  {
    for (int attempt = 0; attempt <= retryCount; attempt++)
    {
      try
      {
        return operation();
      }
      catch (OleDbException ex) when (attempt < retryCount)
      {
        LogRetry(ex, logger, operationName, attempt, retryCount);
        await Task.Delay(RetryDelay);
      }
    }

    throw new UnreachableException("OleDbRetry loop exited without returning or throwing.");
  }

  public static Task ExecuteAsync(Action operation, int retryCount, ILogger logger, string operationName) =>
    ExecuteAsync(() => { operation(); return true; }, retryCount, logger, operationName);

  private static void LogRetry(OleDbException ex, ILogger logger, string operationName, int attempt, int retryCount) =>
    logger.LogWarning(
        ex,
        "{Operation} attempt {Attempt}/{Total} failed; retrying in {DelayMs}ms.",
        operationName,
        attempt + 1,
        retryCount + 1,
        RetryDelay.TotalMilliseconds);
}
