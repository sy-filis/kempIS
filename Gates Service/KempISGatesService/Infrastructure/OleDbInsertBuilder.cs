using System.Data.OleDb;

namespace KempISGatesService.Infrastructure;

// Pairs each column name with its parameter value in one place so the SQL column list and the
// positional parameter list cannot drift apart. Column names are bracket-quoted unconditionally;
// Access/Jet accepts that for any identifier including reserved words like [Key] and [DateTime].
internal sealed class OleDbInsertBuilder(string table)
{
  private readonly List<(string Column, OleDbType Type, object Value)> _columns = [];

  public OleDbInsertBuilder Add(string column, OleDbType type, object value)
  {
    _columns.Add((column, type, value));
    return this;
  }

  public OleDbCommand Build(OleDbConnection connection, OleDbTransaction? transaction = null)
  {
    string columnList = string.Join(", ", _columns.Select(c => $"[{c.Column}]"));
    string placeholders = string.Join(", ", Enumerable.Repeat("?", _columns.Count));
    string sql = $"INSERT INTO [{table}]({columnList}) VALUES ({placeholders})";

    var command = new OleDbCommand(sql, connection, transaction);
    foreach ((_, OleDbType type, object value) in _columns)
    {
      command.Parameters.Add(new OleDbParameter { OleDbType = type, Value = value });
    }
    return command;
  }
}
