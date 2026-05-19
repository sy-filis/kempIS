using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.Queries.GetReservations;
using Domain.Reservations.ReservationStates;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class GetReservationsEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("reservations", async (
      [FromQuery] DateOnly? from,
      [FromQuery] DateOnly? to,
      [FromQuery] ReservationState? status,
      IQueryHandler<GetReservationsQuery, List<ReservationResponse>> handler,
      CancellationToken cancellationToken) =>
    {
      GetReservationsQuery query = new(from, to, status);

      Result<List<ReservationResponse>> result = await handler.Handle(query, cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("GetReservations")
    .WithSummary("List reservations, optionally filtered by date range and status")
    .WithDescription("""
      Returns reservations, optionally filtered by date range and `status`. All filters are
      independent - omitting one does not affect the others.

      **Behavior:** when both `from` and `to` are supplied, only reservations whose stay
      overlaps the inclusive `[from, to]` range are returned. Supplying only `from` returns
      reservations extending to or past that date; supplying only `to` returns reservations
      starting on or before it. With neither, the period filter is skipped. The `status`
      filter accepts any value of the `ReservationState` enum (`Created`, `Confirmed`,
      `CheckedIn`, `Cancelled`, `Completed`); when omitted, every state is returned. Results
      are not paginated.

      **Errors:** `400` invalid date format.
      """)
    .Produces<List<ReservationResponse>>(StatusCodes.Status200OK)
    .ProducesValidationProblem()
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
