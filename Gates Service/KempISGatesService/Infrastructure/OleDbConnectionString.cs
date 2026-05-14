using System.Data.OleDb;

namespace KempISGatesService.Infrastructure;

// Jet 4.0 OleDb connection strings for legacy MDB databases (32-bit only).
public static class OleDbConnectionString
{
  public static string Build(string databaseDirectory, string databaseFileName, string? databasePassword)
  {
    if (string.IsNullOrWhiteSpace(databaseDirectory))
    {
      throw new InvalidOperationException("Databases:DatabaseDirectory must be configured.");
    }

    if (string.IsNullOrWhiteSpace(databaseFileName))
    {
      throw new InvalidOperationException("Database file name must be configured.");
    }

    string fileNameOnly = Path.GetFileName(databaseFileName);
    if (!string.Equals(fileNameOnly, databaseFileName, StringComparison.Ordinal))
    {
      throw new InvalidOperationException("Database file name cannot contain directory traversal characters.");
    }

    string dbPath = Path.Combine(databaseDirectory, fileNameOnly);
    var builder = new OleDbConnectionStringBuilder
    {
      Provider = "Microsoft.Jet.OLEDB.4.0",
      DataSource = dbPath
    };
    // Jet has two distinct credentials. "User ID" / "Password" identify a workgroup-file user
    // (system.mdw); these MDBs use no workgroup file, so the defaults — User=Admin, Password="" —
    // are correct. "Jet OLEDB:Database Password" is the file-level password set on the MDB itself
    // and is the credential we actually need to authenticate. Engine Type 5 = Access 2000 format.
    builder.Add("Password", string.Empty);
    builder.Add("User ID", "Admin");
    builder.Add("Jet OLEDB:Database Password", databasePassword ?? string.Empty);
    builder.Add("Jet OLEDB:Engine Type", 5);

    return builder.ConnectionString;
  }
}
