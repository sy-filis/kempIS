using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.ReservationSpotItems.Commands.GiveKey;
using Application.Reservations.ReservationSpotItems.Commands.ReturnKeys;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class ReservationSpotItemsEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("reservation-spot-items/{id:guid}/return-keys", async (
      Guid id,
      ICommandHandler<ReturnKeysCommand> handler,
      CancellationToken cancellationToken) =>
    {
      Result result = await handler.Handle(new ReturnKeysCommand(id), cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("ReturnKeys")
    .WithSummary("Mark a reservation spot item's keys as returned")
    .WithDescription("""
      Records that the guest has handed back the keys for a single spot item. When this is the
      last outstanding key for the reservation, the parent reservation transitions to its
      completed state.

      **Behavior:** the call is idempotent - re-marking already-returned keys is a no-op
      success. The parent reservation must currently be `CheckedIn` (the gate fires only on the
      transition from not-returned to returned).

      **Errors:** `400` parent reservation is not in `CheckedIn`. `404` reservation spot item or
      its parent reservation does not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager);

    app.MapPost("reservation-spot-items/{id:guid}/give-key", async (
      Guid id,
      ICommandHandler<GiveKeyCommand> handler,
      CancellationToken cancellationToken) =>
    {
      Result result = await handler.Handle(new GiveKeyCommand(id), cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("GiveKey")
    .WithSummary("Mark a reservation spot item's key as handed over")
    .WithDescription("""
      Records that the receptionist has handed the physical key for a single spot item to the
      guest. This is independent of the reservation's check-in state - keys can be handed over
      while the parent reservation is still `Confirmed` (e.g. one cottage's guest has signed in,
      but another hasn't, blocking the reservation-level check-in transition).

      **Behavior:** the call is idempotent - re-calling with the flag already `true` is a no-op
      success. When the bill that pays for this spot is created (via `POST /bills` with
      `reservationSpotItemIds`), the flag is also set automatically, so calling this endpoint
      after billing is never necessary.

      **Errors:** `400` parent reservation is neither `Confirmed` nor `CheckedIn`. `404`
      reservation spot item or its parent reservation does not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
