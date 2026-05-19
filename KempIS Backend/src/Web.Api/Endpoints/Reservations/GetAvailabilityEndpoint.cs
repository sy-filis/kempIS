using Application.Abstractions.Messaging;
using Application.Reservations.Queries.GetAvailability;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class GetAvailabilityEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("availability", async (
      [FromQuery] DateOnly from,
      [FromQuery] DateOnly to,
      [FromQuery] Guid? groupReservationId,
      [FromQuery] string? groupReservationSecret,
      IQueryHandler<GetAvailabilityQuery, AvailabilityResponse> handler,
      CancellationToken cancellationToken) =>
    {
      GetAvailabilityQuery query = new(from, to, groupReservationId, groupReservationSecret);

      Result<AvailabilityResponse> result = await handler.Handle(query, cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("GetAvailability")
    .WithSummary("Get spot availability for a date range")
    .WithDescription("""
      Public, anonymous endpoint used by the booking widget. Returns per-spot-group totals
      (capacity, total spots, occupied, available) plus any events overlapping the period.

      **Behavior:** dates are inclusive. Occupancy combines confirmed/checked-in reservations,
      out-of-orders (per-spot or whole-group), and held spots from other group reservations.
      When a valid `groupReservationId` and matching `groupReservationSecret` are supplied, that
      group's own holds are excluded so members can see capacity available to them.

      **Errors:** `400` invalid date range (`to` before `from`).
      """)
    .Produces<AvailabilityResponse>(StatusCodes.Status200OK)
    .ProducesValidationProblem()
    .AllowAnonymous();
  }
}
