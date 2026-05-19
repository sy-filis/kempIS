using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Database;

internal sealed class ApplicationDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<ApplicationDbContext>
{
  public ApplicationDbContext CreateDbContext(string[] args)
  {
    string connectionString = Environment.GetEnvironmentVariable("DESIGNTIME_DB")
        ?? "Host=localhost;Database=design-time;Username=postgres";

    // IdentityDbContext.OnModelCreating uses IOptions<IdentityOptions> to decide
    // whether to include AspNetUserPasskeys in the model - mirror production here.
    ServiceProvider sp = new ServiceCollection()
        .AddSingleton<SharedKernel.IDateTimeProvider, Time.DateTimeProvider>()
        .AddSingleton<DomainEvents.IDomainEventsDispatcher, DomainEvents.NullDomainEventsDispatcher>()
        .Configure<IdentityOptions>(o => o.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
        .BuildServiceProvider();

    DbContextOptionsBuilder<ApplicationDbContext> optionsBuilder =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable(
                    Microsoft.EntityFrameworkCore.Migrations.HistoryRepository.DefaultTableName,
                    Schemas.Default))
            .UseSnakeCaseNamingConvention()
            .UseApplicationServiceProvider(sp);

    return new ApplicationDbContext(
        optionsBuilder.Options,
        sp.GetRequiredService<DomainEvents.IDomainEventsDispatcher>(),
        sp.GetRequiredService<SharedKernel.IDateTimeProvider>());
  }
}
