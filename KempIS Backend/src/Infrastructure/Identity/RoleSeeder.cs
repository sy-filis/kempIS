using Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

public static class RoleSeeder
{
  public static async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
  {
    foreach (string roleName in Roles.All)
    {
      if (!await roleManager.RoleExistsAsync(roleName))
      {
        await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
      }
    }
  }
}
