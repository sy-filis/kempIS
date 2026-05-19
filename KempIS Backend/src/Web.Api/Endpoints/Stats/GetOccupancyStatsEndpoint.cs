using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.Queries.Stats.GetOccupancyStats;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Stats;

internal sealed class GetOccupancyStatsEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("stats/occupancy", async (
      [FromQuery] DateOnly from,
      [FromQuery] DateOnly to,
      IQueryHandler<GetOccupancyStatsQuery, OccupancyStatsResponse> handler,
      CancellationToken cancellationToken) =>
    {
      GetOccupancyStatsQuery query = new(from, to);
      Result<OccupancyStatsResponse> result = await handler.Handle(query, cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Stats)
    .WithName("GetOccupancyStats")
    .WithSummary("Spot-group occupancy: occupied vs configured-capacity spot-nights")
    .WithDescription("""
      For each spot group with reservations overlapping `[from, to]`, returns the
      number of occupied spot-nights vs `SpotGroup.Capacity × nightsInRange`, plus
      a one-decimal occupancy percentage.

      Only reservations in state `Confirmed`, `CheckedIn`, or `Completed` are
      counted. `ReservationSpotItem` rows contribute regardless of whether
      `SpotId` is assigned. Inactive spot groups with occupancy are returned
      (`isActive` exposed on each row). Spot groups with zero occupancy in the
      range are omitted. OutOfOrder periods are not deducted from capacity.

      **Errors:** `400` when `to < from` or when the range spans more than 366 days.
      """)
    .Produces<OccupancyStatsResponse>(StatusCodes.Status200OK)
    .ProducesValidationProblem()
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
