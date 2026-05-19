using Application.Abstractions.Authentication;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Auth;

internal sealed class LogoutEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("auth/logout", async (
      IUserContext userContext,
      UserManager<ApplicationUser> userManager) =>
    {
      ApplicationUser? user = await userManager.FindByIdAsync(userContext.UserId.ToString());
      if (user is null)
      {
        return CustomResults.Problem(Result.Failure(AuthErrors.UserNotFound));
      }

      IdentityResult stampResult = await userManager.UpdateSecurityStampAsync(user);
      if (!stampResult.Succeeded)
      {
        return CustomResults.Problem(
            Result.Failure(
                AuthErrors.IdentityFailure(
                    string.Join("; ", stampResult.Errors.Select(e => e.Description)))));
      }

      return Results.NoContent();
    })
    .WithTags(Tags.Auth)
    .WithName("AuthLogout")
    .WithSummary("Log out the current user (rotates security stamp)")
    .WithDescription("""
      Logs out the calling user by rotating their ASP.NET Identity security stamp. Any
      bearer access token already in flight remains valid until its short expiry, but every
      previously issued refresh token is immediately invalidated because the stamp embedded
      in those tokens no longer matches.

      **Side effects:** updates `AspNetUsers.SecurityStamp`. The next call to `auth/refresh`
      with an old refresh token will return `401`.

      **Errors:** `400` the underlying ASP.NET Identity stamp update failed (e.g. concurrent
      modification). `404` the authenticated principal references a user id that no longer
      exists in the user store.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .RequireAuthorization();
  }
}
