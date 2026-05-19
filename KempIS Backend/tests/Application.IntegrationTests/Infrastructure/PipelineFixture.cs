using Application;
using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Reservations;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel;
using TestUtilities.Fakes;

namespace Application.IntegrationTests.Infrastructure;

public sealed class PipelineFixture : IAsyncDisposable
{
  public SqliteConnection Connection { get; }
  public ApplicationDbContext Db { get; }
  public FakeDateTimeProvider Clock { get; } = new();
  public ListLoggerProvider Logs { get; } = new();
  public ISpotAvailabilityChecker AvailabilityChecker { get; } = Substitute.For<ISpotAvailabilityChecker>();
  public IReservationNumberGenerator NumberGenerator { get; } = Substitute.For<IReservationNumberGenerator>();
  public IGroupReservationNumberGenerator GroupReservationNumberGenerator { get; } = Substitute.For<IGroupReservationNumberGenerator>();
  public ServiceProvider Services { get; }

  public PipelineFixture()
  {
    Connection = new SqliteConnection("DataSource=:memory:");
    Connection.Open();

    using (SqliteCommand pragma = Connection.CreateCommand())
    {
      pragma.CommandText = "PRAGMA foreign_keys = OFF;";
      pragma.ExecuteNonQuery();
    }

    DbContextOptions<ApplicationDbContext> options =
      new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite(Connection)
        .UseSnakeCaseNamingConvention()
        .Options;

    Db = new ApplicationDbContext(options, NullDomainEventsDispatcher.Instance, new TestUtilities.Fakes.FakeDateTimeProvider());
    Db.Database.EnsureCreated();

    ServiceCollection services = new();
    services.AddApplication();
    services.AddSingleton<INoAuthState>(new StubNoAuthState(IsEnabled: false));
    services.AddLogging(b => b.AddProvider(Logs).SetMinimumLevel(LogLevel.Information));

    services.Replace(ServiceDescriptor.Scoped<ApplicationDbContext>(_ => Db));
    services.Replace(ServiceDescriptor.Scoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>()));
    services.Replace(ServiceDescriptor.Scoped<IDateTimeProvider>(_ => Clock));
    services.Replace(ServiceDescriptor.Scoped(_ => AvailabilityChecker));
    NumberGenerator.NextAsync(default, default).ReturnsForAnyArgs(_ => Task.FromResult("R-2026/0001"));
    services.AddScoped(_ => NumberGenerator);
    GroupReservationNumberGenerator.NextAsync(default, default).ReturnsForAnyArgs(_ => Task.FromResult("GR-2026/0001"));
    services.AddScoped(_ => GroupReservationNumberGenerator);
    services.TryAddSingleton<IDomainEventsDispatcher>(NullDomainEventsDispatcher.Instance);

    Services = services.BuildServiceProvider();
  }

  public IServiceScope CreateScope() => Services.CreateScope();

  public async ValueTask DisposeAsync()
  {
    await Services.DisposeAsync();
    await Db.DisposeAsync();
    await Connection.DisposeAsync();
  }
}

internal sealed record StubNoAuthState(bool IsEnabled) : INoAuthState;
