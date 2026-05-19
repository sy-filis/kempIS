using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Operations.CleaningPlans;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Operations;

internal sealed class CleaningPlansEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("cleaning-plans")
      .WithTags(Tags.Operations)
      .HasRole(Roles.Receptionist, Roles.CleaningStaff, Roles.Manager);

    group.MapGet("{date}", async (
      DateOnly date,
      IQueryHandler<GetCleaningPlanByDateQuery, CleaningPlanDetailResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<CleaningPlanDetailResponse> result =
        await handler.Handle(new GetCleaningPlanByDateQuery(date), cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetCleaningPlanByDate")
    .WithSummary("Get the cleaning plan for a date")
    .WithDescription("""
      Returns the cleaning plan for the supplied date together with its clean-info entries.
      If no plan exists for the date, an empty plan is created on the fly with
      `updatedAtUtc` and `updatedByUserId` set to `null` - those fields populate on the
      first mutation. Spots are added via `POST /cleaning-plans/{date}/clean-infos`. Every
      mutation (add, remove, mark-cleaned, patch note) bumps `updatedAtUtc` and
      `updatedByUserId`.
      """)
    .Produces<CleaningPlanDetailResponse>(StatusCodes.Status200OK);
  }
}
