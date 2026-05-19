using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.Commands.CancelReservation;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class CancelReservationEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("reservations/{id:guid}/cancel", async (
      Guid id,
      ICommandHandler<CancelReservationCommand> handler,
      CancellationToken cancellationToken) =>
    {
      CancelReservationCommand command = new(id);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("CancelReservation")
    .WithSummary("Cancel a reservation")
    .WithDescription("""
      Marks a reservation as `Cancelled`, releasing its hold on the assigned spots so the period
      becomes available again.

      **Behavior:** any state other than `Cancelled` is accepted. Cancelling an
      already-cancelled reservation returns `400` rather than succeeding silently - clients
      should treat the second call as a no-op and not retry.

      **Errors:** `400` the reservation is already cancelled. `404` reservation does not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
