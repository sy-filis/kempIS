using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.Queries.GetMonthlyReservationSummary;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class GetMonthlyReservationSummaryEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("reservations/monthly-summary", async (
      [FromQuery] int year,
      IQueryHandler<GetMonthlyReservationSummaryQuery, MonthlyReservationSummaryResponse> handler,
      CancellationToken cancellationToken) =>
    {
      GetMonthlyReservationSummaryQuery query = new(year);

      Result<MonthlyReservationSummaryResponse> result = await handler.Handle(query, cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("GetMonthlyReservationSummary")
    .WithSummary("Per-month reservation count for a year")
    .WithDescription("""
      Returns a 12-element array of reservation counts, one per month (Jan..Dec), for the
      requested calendar `year`.

      A reservation contributes to *every* month its stay overlaps within the year, so a
      stay spanning March 30 → April 4 increments both March and April. Periods extending
      outside the requested year are clamped to year boundaries before expansion. Only
      reservations in state `Confirmed`, `CheckedIn`, or `Completed` are counted; `Created`
      and `Cancelled` are excluded.

      **Errors:** `400` when `year` is outside the supported `[2000, 2100]` range.
      """)
    .Produces<MonthlyReservationSummaryResponse>(StatusCodes.Status200OK)
    .ProducesValidationProblem()
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
