using Application.Abstractions.Messaging;
using Application.Auth.Queries.GetCurrentUser;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Auth;

internal sealed class CurrentUserEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("auth/me", async (
      IQueryHandler<GetCurrentUserQuery, CurrentUserResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<CurrentUserResponse> result = await handler.Handle(new GetCurrentUserQuery(), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Auth)
    .WithName("AuthCurrentUser")
    .WithSummary("Get the calling user's identity")
    .WithDescription("""
      Returns the authenticated user's id, username, display name, and the roles they currently
      hold. The bearer tokens issued by this API are opaque (ASP.NET Identity data-protection
      tokens), so the frontend cannot derive these values from the token itself - this endpoint
      is the canonical "who am I" lookup and reflects the live state of the user record (roles
      added or removed mid-session are observed on the next call).

      **Errors:** `401` no authenticated principal. `404` the authenticated principal references
      a user id that no longer exists in the user store.
      """)
    .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .RequireAuthorization();
  }
}
