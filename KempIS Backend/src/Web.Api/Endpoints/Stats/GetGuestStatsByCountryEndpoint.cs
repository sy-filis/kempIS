using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.Queries.Stats.GetGuestStatsByCountry;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Stats;

internal sealed class GetGuestStatsByCountryEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("stats/guests/by-country", async (
      [FromQuery] DateOnly from,
      [FromQuery] DateOnly to,
      IQueryHandler<GetGuestStatsByCountryQuery, GuestStatsByCountryResponse> handler,
      CancellationToken cancellationToken) =>
    {
      GetGuestStatsByCountryQuery query = new(from, to);
      Result<GuestStatsByCountryResponse> result = await handler.Handle(query, cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Stats)
    .WithName("GetGuestStatsByCountry")
    .WithSummary("Guest counts and person-nights grouped by nationality")
    .WithDescription("""
      Counts billed guests whose bill check-in/check-out window overlaps the requested
      `[from, to]` range, grouped by `Guest.NationalityId` (citizenship). For each
      guest the contributed person-nights equal the number of stay nights that fall
      inside the range - i.e. `max(0, min(billCheckOut, to+1) - max(billCheckIn, from))`.

      Inline guests not linked to a bill are excluded. Rows are sorted by
      `personNights` desc, then `alpha3` asc.

      **Errors:** `400` when `to < from` or when the range spans more than 366 days.
      """)
    .Produces<GuestStatsByCountryResponse>(StatusCodes.Status200OK)
    .ProducesValidationProblem()
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
