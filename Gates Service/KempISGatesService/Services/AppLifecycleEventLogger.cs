using System.Data.OleDb;
using KempISGatesService.Data;
using KempISGatesService.Infrastructure;
using KempISGatesService.Models;
using KempISGatesService.Options;

using Microsoft.Extensions.Options;

namespace KempISGatesService.Services;

// Logs application startup and shutdown events to the Events MDB.
public sealed class AppLifecycleEventLogger(
    IEventRepository repository,
    IOptions<DatabaseOptions> options,
    ILogger<AppLifecycleEventLogger> logger)
{
  private readonly int _retryCount = options.Value.RetryCount;

  public void LogProgramBegin()
  {
    if (!TryInsertLifecycleEvent(EventOperation.ProgramBegin))
    {
      logger.LogError("Unable to persist program begin event.");
    }
  }

  public void LogProgramEnd()
  {
    if (!TryInsertLifecycleEvent(EventOperation.ProgramEnd))
    {
      logger.LogError("Unable to persist program end event.");
    }
  }

  private bool TryInsertLifecycleEvent(EventOperation operation)
  {
    try
    {
      OleDbRetry.Execute(
          () => repository.InsertLifecycleEvent(operation),
          _retryCount,
          logger,
          "Lifecycle event insert");

      logger.LogInformation("Successfully inserted application lifecycle event {Operation}.", operation);
      return true;
    }
    catch (OleDbException ex)
    {
      logger.LogError(ex, "Failed to write application lifecycle event {Operation}.", operation);
      return false;
    }
  }
}
