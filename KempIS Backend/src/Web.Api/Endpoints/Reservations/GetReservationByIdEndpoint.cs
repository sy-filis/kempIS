using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.Queries.GetReservationById;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class GetReservationByIdEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("reservations/{id:guid}", async (
      Guid id,
      IQueryHandler<GetReservationByIdQuery, ReservationDetailResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<ReservationDetailResponse> result =
        await handler.Handle(new GetReservationByIdQuery(id), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("GetReservationById")
    .WithSummary("Get a reservation by id")
    .WithDescription("""
      Returns the full detail of a single reservation: header info, guests, spot items,
      service items, meals, invoices, related bills, and access cards. Bills are included
      both directly (linked via `Bill.ReservationId`) and indirectly through invoices on the
      reservation.

      **Errors:** `404` reservation does not exist.
      """)
    .Produces<ReservationDetailResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
