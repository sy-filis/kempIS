using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.Commands.CheckInReservation;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class CheckInReservationEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("reservations/{id:guid}/check-in", async (
      Guid id,
      ICommandHandler<CheckInReservationCommand> handler,
      CancellationToken cancellationToken) =>
    {
      CheckInReservationCommand command = new(id);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("CheckInReservation")
    .WithSummary("Check in a reservation")
    .WithDescription("""
      Transitions a reservation to the `CheckedIn` state once the guest party arrives on site.

      **Behavior:** the reservation must currently be in `Created` or `Confirmed`. Every non-Czech
      guest on the reservation must already have a stored signature (Czech guests are exempt by
      law); missing signatures block the transition with the failing guest ids attached.

      **Errors:** `400` reservation is not in a check-in eligible state, or one or more
      non-Czech guests still need to sign. `404` reservation does not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
