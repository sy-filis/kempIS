using System.Data.OleDb;
using KempISGatesService.Infrastructure;
using KempISGatesService.Models;
using KempISGatesService.Options;
using Microsoft.Extensions.Options;

namespace KempISGatesService.Data;

// Throws on DB failure; performs no logging.
public sealed class EventRepository : IEventRepository
{
  // Legacy sentinels mirrored from the original gate application.
  private const int NonNumericEventName = -1;
  private const string LifecycleEventId = "0";

  private readonly string _eventsConnectionString;
  private readonly string _eventOperator;

  public EventRepository(IOptions<DatabaseOptions> options)
  {
    DatabaseOptions configuredOptions = options.Value;
    _eventsConnectionString = OleDbConnectionString.Build(
        configuredOptions.DatabaseDirectory,
        configuredOptions.EventsDatabaseFileName,
        configuredOptions.DatabasePassword);
    _eventOperator = configuredOptions.EventOperator;
  }

  public void InsertCardEvent(string keyValue, EventOperation operation, string realName, SenzorId senzorId) =>
    Insert(idValue: keyValue, operation, realName, accountTotal: 0, senzorId);

  // Lifecycle rows use Id=LifecycleEventId and write NULL into AccountTotal instead of 0.
  public void InsertLifecycleEvent(EventOperation operation) =>
    Insert(idValue: LifecycleEventId, operation, realName: string.Empty, accountTotal: null, SenzorId.ApplicationLifecycleEvent);

  public void Probe()
  {
    using var eventsConnection = new OleDbConnection(_eventsConnectionString);
    eventsConnection.Open();
  }

  private void Insert(string idValue, EventOperation operation, string realName, int? accountTotal, SenzorId senzorId)
  {
    using var eventsConnection = new OleDbConnection(_eventsConnectionString);
    eventsConnection.Open();

    using OleDbCommand insertCommand = new OleDbInsertBuilder("Events")
        .Add("AccountChange", OleDbType.Integer, 0)
        .Add("AccountChangeVAT", OleDbType.Integer, 0)
        .Add("AccountTotal", OleDbType.Integer, (object?)accountTotal ?? DBNull.Value)
        .Add("DateTime", OleDbType.Integer, LegacyTime.ToSeconds(DateTimeOffset.Now))
        .Add("Id", OleDbType.VarWChar, idValue)
        .Add("Name", OleDbType.Integer, NonNumericEventName)
        .Add("Operation", OleDbType.Integer, (int)operation)
        .Add("Operator", OleDbType.VarWChar, _eventOperator)
        .Add("RealName", OleDbType.VarWChar, realName)
        .Add("SenzorsId", OleDbType.Integer, (int)senzorId)
        .Add("UseCredit", OleDbType.Boolean, true)
        .Build(eventsConnection);
    _ = insertCommand.ExecuteNonQuery();
  }
}
