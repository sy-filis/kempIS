using Infrastructure.Database;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Web.Api.Extensions;

public static class MigrationExtensions
{
  public static void ApplyMigrations(this IApplicationBuilder app)
  {
    using IServiceScope scope = app.ApplicationServices.CreateScope();

    using ApplicationDbContext dbContext =
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    dbContext.Database.Migrate();
  }

  public static async Task SeedRolesAsync(this IApplicationBuilder app)
  {
    using IServiceScope scope = app.ApplicationServices.CreateScope();

    RoleManager<ApplicationRole> roleManager =
        scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

    await RoleSeeder.SeedRolesAsync(roleManager);
  }
}
