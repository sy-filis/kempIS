using Infrastructure.Database;
using Infrastructure.Seed;
using Microsoft.Extensions.Logging;

namespace Web.Api.Extensions;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class TestDataSeedExtensions
{
  public static async Task SeedTestDataAsync(this IApplicationBuilder app, CancellationToken cancellationToken = default)
  {
    using IServiceScope scope = app.ApplicationServices.CreateScope();

    ApplicationDbContext dbContext =
      scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    ILogger<TestDataSeeder> logger =
      scope.ServiceProvider.GetRequiredService<ILogger<TestDataSeeder>>();

    TestDataSeeder seeder = new(dbContext, logger);
    await seeder.SeedAsync(cancellationToken);
  }
}
