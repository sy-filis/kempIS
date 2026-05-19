using System.Collections.Concurrent;
using Application.Abstractions.Authentication;
using Application.Abstractions.Email;
using Application.Abstractions.Reservations;
using Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TestUtilities.Fakes;

namespace Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Preserves the real ASP.NET Identity bearer-token scheme (unlike <see cref="ApiFactory"/>)
/// and injects a <see cref="FakeTimeProvider"/> so tests can advance past refresh-token expiry.
/// </summary>
public sealed class AuthLifecycleApiFactory : WebApplicationFactory<Program>
{
  private readonly SqliteConnection _connection;

  public FakePasskeyAuthenticator PasskeyAuthenticator { get; } = new();
  public CapturingEmailSender EmailSender { get; } = new();
  public FakeTimeProvider TimeProvider { get; } = new();
  public ConcurrentQueue<Exception> ServerExceptions { get; } = new();

  public AuthLifecycleApiFactory()
  {
    _connection = new SqliteConnection("DataSource=:memory:");
    _connection.Open();
    using SqliteCommand pragma = _connection.CreateCommand();
    pragma.CommandText = "PRAGMA foreign_keys = OFF;";
    pragma.ExecuteNonQuery();

    DbContextOptions<ApplicationDbContext> options =
      new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite(_connection)
        .UseSnakeCaseNamingConvention()
        .Options;

    using var initContext = new ApplicationDbContext(options, NullDomainEventsDispatcher.Instance, new TestUtilities.Fakes.FakeDateTimeProvider());
    initContext.Database.EnsureCreated();

    // IdentitySchemaVersions.Version3 (AspNetUserPasskeys table) is wired by AddIdentityCore;
    // the bare DbContext above stays on V2, so create the table manually for tests.
    using SqliteCommand passkeysTable = _connection.CreateCommand();
    passkeysTable.CommandText = """
      CREATE TABLE IF NOT EXISTS "AspNetUserPasskeys" (
        credential_id BLOB NOT NULL PRIMARY KEY,
        user_id TEXT NOT NULL,
        data TEXT NOT NULL,
        FOREIGN KEY (user_id) REFERENCES "AspNetUsers" (id) ON DELETE CASCADE
      );
      CREATE INDEX IF NOT EXISTS ix_asp_net_user_passkeys_user_id
        ON "AspNetUserPasskeys" (user_id);
      """;
    passkeysTable.ExecuteNonQuery();

    ClientOptions.BaseAddress = new Uri("http://localhost/api/");
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    builder.UseEnvironment("Test");

    builder.ConfigureAppConfiguration((_, config) =>
    {
      config.AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["ConnectionStrings:Database"] = "DataSource=:memory:",
        ["Email:Host"] = "localhost",
        ["Email:Port"] = "25",
        ["Email:From"] = "test@example.com",
        ["Email:FromName"] = "Test",
        ["Ubyport:EndpointUrl"] = "https://ubyport.pcr.cz/ws_uby_test/ws_uby.svc",
        ["Ubyport:Username"] = "u",
        ["Ubyport:Password"] = "p",
        ["Ubyport:AuthenticationCode"] = "code",
        ["Ubyport:IdUb"] = "1",
        ["Ubyport:Mark"] = "MARK",
        ["Ubyport:Name"] = "Test Accommodation",
        ["Ubyport:Contact"] = "test@example.com",
        ["Ubyport:District"] = "Praha",
        ["Ubyport:Town"] = "Praha",
        ["Ubyport:Street"] = "Lennonova",
        ["Ubyport:HouseNumber"] = "1",
        ["Ubyport:Zip"] = "10000",
      });
    });

    builder.ConfigureServices(services =>
    {
      services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
      services.RemoveAll<DbContextOptions>();
      services.RemoveAll<ApplicationDbContext>();
      services.RemoveAll(typeof(Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<ApplicationDbContext>));
      services.AddDbContext<ApplicationDbContext>(o => o
        .UseSqlite(_connection)
        .UseSnakeCaseNamingConvention());

      // Real IIdentityService kept so it hits the real UserManager; only passkey crypto is faked.
      services.Replace(ServiceDescriptor.Scoped<IPasskeyAuthenticator>(_ => PasskeyAuthenticator));
      services.Replace(ServiceDescriptor.Singleton<IEmailSender>(_ => EmailSender));
      IEmailTemplateRenderer renderer = Substitute.For<IEmailTemplateRenderer>();
      renderer.RenderAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(SharedKernel.Result.Success(new RenderedEmail("Test subject", "Test body")));
      services.Replace(ServiceDescriptor.Singleton(renderer));
      services.Replace(ServiceDescriptor.Scoped<ISpotAvailabilityChecker>(_ => Substitute.For<ISpotAvailabilityChecker>()));

      services.RemoveAll<TimeProvider>();
      services.AddSingleton<TimeProvider>(TimeProvider);

      services.RemoveAll<Microsoft.AspNetCore.Diagnostics.IExceptionHandler>();
      services.AddSingleton<Microsoft.AspNetCore.Diagnostics.IExceptionHandler>(
        _ => new ExceptionCapturingHandler(ServerExceptions));
    });
  }

  protected override void Dispose(bool disposing)
  {
    if (disposing)
    {
      _connection.Dispose();
    }
    base.Dispose(disposing);
  }

  public async Task WithDbAsync(Func<ApplicationDbContext, Task> action)
  {
    using IServiceScope scope = Services.CreateScope();
    ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await action(db);
  }

  public async Task WithScopeAsync(Func<IServiceProvider, Task> action)
  {
    using IServiceScope scope = Services.CreateScope();
    await action(scope.ServiceProvider);
  }
}
