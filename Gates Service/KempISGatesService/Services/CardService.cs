using System.Data.OleDb;
using System.Globalization;
using KempISGatesService.Data;
using KempISGatesService.Infrastructure;
using KempISGatesService.Models;
using KempISGatesService.Options;

using Microsoft.Extensions.Options;

namespace KempISGatesService.Services;

// Pairs card-row writes with their matching audit-event row in the legacy MDB.
public sealed class CardService(
  ICardRepository cardRepository,
  IEventRepository eventRepository,
  IOptions<DatabaseOptions> options,
  ILogger<CardService> logger)
{
  // Per-key serialization: closes the exists-check / delete / insert TOCTOU window and keeps
  // audit-event ordering consistent when concurrent requests target the same card. Entries are
  // refcounted under _keyLocksGate so the slot is dropped once the last waiter releases.
  private readonly Dictionary<int, KeyLock> _keyLocks = new();
  private readonly Lock _keyLocksGate = new();
  private readonly int _retryCount = options.Value.RetryCount;

  public async Task<CardWriteResult> UpsertCardAsync(int key, DateTimeOffset validTo, string realName, string note)
  {
    KeyLock keyLock = AcquireKeyLock(key);
    await keyLock.Semaphore.WaitAsync();
    try
    {
      string keyValue = key.ToString(CultureInfo.InvariantCulture);
      CardUpsertOutcome outcome;

      try
      {
        outcome = await OleDbRetry.ExecuteAsync(
            () => cardRepository.Upsert(key, validTo, realName, note),
            _retryCount,
            logger,
            "Card upsert");
      }
      catch (OleDbException ex)
      {
        logger.LogError(ex, "Failed to write card state to Users database for key {CardKey}.", key);
        return CardWriteResult.DatabaseError;
      }

      EventOperation operation = outcome == CardUpsertOutcome.Updated ? EventOperation.CardChanged : EventOperation.CardCreated;
      logger.LogInformation("Successfully upserted card {CardKey} in Users database (operation: {Operation}).", key, operation);

      try
      {
        await OleDbRetry.ExecuteAsync(
            () => eventRepository.InsertCardEvent(keyValue, operation, realName, SenzorId.CardCreated),
            _retryCount,
            logger,
            "Card event insert");
        logger.LogInformation("Successfully inserted event {Operation} for card {CardKey}.", operation, key);
      }
      catch (OleDbException ex)
      {
        logger.LogError(ex, "Users write succeeded for key {CardKey}, but writing event log failed.", key);
        return CardWriteResult.DatabaseError;
      }

      return CardWriteResult.Success;
    }
    finally
    {
      keyLock.Semaphore.Release();
      ReleaseKeyLock(key, keyLock);
    }
  }

  // Returns NotFound and skips the audit event if no card matched.
  public async Task<CardWriteResult> DeleteCardAsync(int key)
  {
    KeyLock keyLock = AcquireKeyLock(key);
    await keyLock.Semaphore.WaitAsync();
    try
    {
      string keyValue = key.ToString(CultureInfo.InvariantCulture);

      try
      {
        bool deleted = await OleDbRetry.ExecuteAsync(
            () => cardRepository.Delete(key),
            _retryCount,
            logger,
            "Card delete");
        if (!deleted)
        {
          return CardWriteResult.NotFound;
        }
        logger.LogInformation("Successfully marked card {CardKey} as deleted in Users database.", key);
      }
      catch (OleDbException ex)
      {
        logger.LogError(ex, "Failed to delete card from Users database for key {CardKey}.", key);
        return CardWriteResult.DatabaseError;
      }

      try
      {
        await OleDbRetry.ExecuteAsync(
            () => eventRepository.InsertCardEvent(keyValue, EventOperation.CardDeleted, string.Empty, SenzorId.CardDeleted),
            _retryCount,
            logger,
            "Card event insert");
        logger.LogInformation("Successfully inserted event {Operation} for card {CardKey}.", EventOperation.CardDeleted, key);
      }
      catch (OleDbException ex)
      {
        logger.LogError(ex, "Users delete succeeded for key {CardKey}, but writing event log failed.", key);
        return CardWriteResult.DatabaseError;
      }

      return CardWriteResult.Success;
    }
    finally
    {
      keyLock.Semaphore.Release();
      ReleaseKeyLock(key, keyLock);
    }
  }

  private KeyLock AcquireKeyLock(int key)
  {
    lock (_keyLocksGate)
    {
      if (!_keyLocks.TryGetValue(key, out KeyLock? keyLock))
      {
        keyLock = new KeyLock();
        _keyLocks[key] = keyLock;
      }
      keyLock.RefCount++;
      return keyLock;
    }
  }

  private void ReleaseKeyLock(int key, KeyLock keyLock)
  {
    lock (_keyLocksGate)
    {
      if (--keyLock.RefCount == 0)
      {
        _keyLocks.Remove(key);
        keyLock.Semaphore.Dispose();
      }
    }
  }

  private sealed class KeyLock
  {
    public SemaphoreSlim Semaphore { get; } = new(1, 1);

    public int RefCount { get; set; }
  }
}
