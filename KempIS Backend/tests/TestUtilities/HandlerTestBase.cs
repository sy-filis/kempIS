using Infrastructure.Database;
using Microsoft.Data.Sqlite;
using TestUtilities.Fakes;

namespace TestUtilities;

/// <summary>
/// Domain events raised during SaveChangesAsync are discarded by the default dispatcher.
/// Snapshot events on the entity before saving, or override <see cref="CreateDbContext"/>
/// to inject a <see cref="CapturingDomainEventsDispatcher"/>.
/// </summary>
public abstract class HandlerTestBase : IAsyncLifetime
{
  private SqliteConnection _connection = null!;

  protected ApplicationDbContext Db { get; private set; } = null!;

  protected FakeDateTimeProvider Clock { get; } = new();

  public virtual async Task InitializeAsync()
  {
    _connection = new SqliteConnection("DataSource=:memory:");
    await _connection.OpenAsync();

    await using (SqliteCommand pragma = _connection.CreateCommand())
    {
      pragma.CommandText = "PRAGMA foreign_keys = OFF;";
      await pragma.ExecuteNonQueryAsync();
    }

    Db = CreateDbContext(_connection);

    await Db.Database.EnsureCreatedAsync();
  }

  public virtual async Task DisposeAsync()
  {
    await Db.DisposeAsync();
    await _connection.DisposeAsync();
  }

  protected virtual ApplicationDbContext CreateDbContext(SqliteConnection connection)
  {
    DbContextOptions<ApplicationDbContext> options =
      new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite(connection)
        .UseSnakeCaseNamingConvention()
        .Options;

    return new ApplicationDbContext(options, NullDomainEventsDispatcher.Instance, Clock);
  }
}
