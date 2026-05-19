using Infrastructure.Database;
using Infrastructure.Seed;
using Microsoft.Extensions.Logging;

namespace Web.Api.Extensions;

public static class ReferenceDataSeedExtensions
{
  public static async Task SeedReferenceDataAsync(this IApplicationBuilder app, CancellationToken cancellationToken = default)
  {
    using IServiceScope scope = app.ApplicationServices.CreateScope();

    ApplicationDbContext dbContext =
      scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    ILogger<ReferenceDataSeeder> logger =
      scope.ServiceProvider.GetRequiredService<ILogger<ReferenceDataSeeder>>();

    ReferenceDataSeeder seeder = new(dbContext, logger);
    await seeder.SeedAsync(cancellationToken);
  }
}
