using System.Data.OleDb;
using System.Globalization;
using KempISGatesService.Infrastructure;
using KempISGatesService.Models;
using KempISGatesService.Options;
using Microsoft.Extensions.Options;

namespace KempISGatesService.Data;

// Throws on DB failure; performs no logging and no audit-event writes.
public sealed class CardRepository : ICardRepository
{
  private const string CardHardDeleteSql = "DELETE FROM Card WHERE ([Key] = ?)";
  private const string CardExistsSql = "SELECT COUNT(*) FROM Card WHERE ([Key] = ?)";

  private readonly string _usersConnectionString;

  public CardRepository(IOptions<DatabaseOptions> options)
  {
    DatabaseOptions configuredOptions = options.Value;
    _usersConnectionString = OleDbConnectionString.Build(
        configuredOptions.DatabaseDirectory,
        configuredOptions.UsersDatabaseFileName,
        configuredOptions.DatabasePassword);
  }

  // Preserves the legacy exists-check → hard-delete → insert pattern.
  public CardUpsertOutcome Upsert(int key, DateTimeOffset validTo, string realName, string note)
  {
    string keyValue = key.ToString(CultureInfo.InvariantCulture);
    int validFromSeconds = LegacyTime.ToSeconds(DateTimeOffset.Now);
    int validToSeconds = LegacyTime.ToSeconds(validTo);

    using var usersConnection = new OleDbConnection(_usersConnectionString);
    usersConnection.Open();

    // Atomic exists → delete → insert so a mid-sequence failure cannot leave the row deleted.
    using OleDbTransaction transaction = usersConnection.BeginTransaction();

    bool cardAlreadyExists;
    using (var existsCommand = new OleDbCommand(CardExistsSql, usersConnection, transaction))
    {
      existsCommand.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = keyValue });
      object? existsValue = existsCommand.ExecuteScalar();
      cardAlreadyExists = existsValue != null && existsValue != DBNull.Value && Convert.ToInt32(existsValue, CultureInfo.InvariantCulture) > 0;
    }

    if (cardAlreadyExists)
    {
      using var deleteCommand = new OleDbCommand(CardHardDeleteSql, usersConnection, transaction);
      deleteCommand.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = keyValue });
      _ = deleteCommand.ExecuteNonQuery();
    }

    using (OleDbCommand insertCommand = new OleDbInsertBuilder("Card")
        .Add("AccountState", OleDbType.Integer, 0)
        .Add("Address", OleDbType.VarWChar, string.Empty)
        .Add("Company", OleDbType.VarWChar, string.Empty)
        .Add("DeposidOnCard", OleDbType.Integer, 0)
        .Add("DIC", OleDbType.Integer, 0)
        .Add("DOB", OleDbType.DBDate, DBNull.Value)
        .Add("Id", OleDbType.Integer, 0)
        .Add("IdentityCard", OleDbType.Integer, 0)
        .Add("IdGroupUsers", OleDbType.Integer, 0)
        .Add("IsDeleted", OleDbType.Boolean, false)
        .Add("Key", OleDbType.VarWChar, keyValue)
        .Add("MinValueAccount", OleDbType.Integer, 0)
        .Add("Name", OleDbType.VarWChar, realName)
        .Add("Notes", OleDbType.VarWChar, note)
        .Add("Pos", OleDbType.Integer, 0)
        .Add("PriceForStay", OleDbType.Integer, 0)
        .Add("PrivateId", OleDbType.VarWChar, string.Empty)
        .Add("Profil", OleDbType.Integer, 0)
        .Add("Saldo", OleDbType.SmallInt, (short)0)
        .Add("States", OleDbType.Integer, 0)
        .Add("StayPriceType", OleDbType.Integer, 0)
        .Add("UseCredit", OleDbType.Boolean, false)
        .Add("ValidFrom", OleDbType.Integer, validFromSeconds)
        .Add("ValidTo", OleDbType.Integer, validToSeconds)
        .Add("IsUnit", OleDbType.Boolean, false)
        .Build(usersConnection, transaction))
    {
      _ = insertCommand.ExecuteNonQuery();
    }

    transaction.Commit();

    return cardAlreadyExists ? CardUpsertOutcome.Updated : CardUpsertOutcome.Inserted;
  }

  public bool Delete(int key)
  {
    string keyValue = key.ToString(CultureInfo.InvariantCulture);

    using var usersConnection = new OleDbConnection(_usersConnectionString);
    usersConnection.Open();

    using var deleteCommand = new OleDbCommand(CardHardDeleteSql, usersConnection);
    deleteCommand.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = keyValue });

    int affectedRows = deleteCommand.ExecuteNonQuery();
    return affectedRows > 0;
  }

  public void Probe()
  {
    using var usersConnection = new OleDbConnection(_usersConnectionString);
    usersConnection.Open();
  }
}
