using Application.Abstractions.Messaging;
using Application.Reservations.Commands.CreateWebReservation;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class CreateWebReservationEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("reservations/web", async (
      CreateWebReservationRequest request,
      ICommandHandler<CreateWebReservationCommand, CreateWebReservationResponse> handler,
      CancellationToken cancellationToken) =>
    {
      CreateWebReservationCommand command = new(
        request.Name,
        request.Surname,
        request.Email,
        request.Phone,
        request.From,
        request.To,
        request.RequestedSpots,
        request.Note,
        request.GroupReservationId,
        request.GroupReservationSecret,
        Language: request.Language);

      Result<CreateWebReservationResponse> result = await handler.Handle(command, cancellationToken);

      return result.Match(
        response => Results.Created($"/reservations/{response.Id}", response),
        CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("CreateWebReservation")
    .WithSummary("Create a reservation from the public booking widget")
    .WithDescription("""
      Public, anonymous endpoint used by the booking widget. The caller requests a quantity per
      spot group rather than naming concrete spots; staff resolve those into specific spots
      later by editing the reservation, which transitions a `Created` reservation to
      `Confirmed`.

      **Behavior:** every requested spot group must exist and be active, and the requested
      quantity must fit within the available capacity (spots minus existing confirmed/checked-in
      reservations, out-of-orders, and other group holds) for the period. The new reservation
      starts in the `Created` state. When `groupReservationId` is supplied, the matching
      `groupReservationSecret` must be provided, the group must still be `Created`, and the
      requested period must overlap the group's period.

      **Errors:** `400` invalid payload, group reservation is cancelled, secret does not match,
      requested period falls outside the group period, requested spot group is inactive, or
      requested quantity exceeds the group's available capacity. `404` requested spot group or
      referenced group reservation does not exist.
      """)
    .Produces<CreateWebReservationResponse>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .AllowAnonymous();
  }
}

internal sealed record CreateWebReservationRequest(
  string Name,
  string Surname,
  string Email,
  string Phone,
  DateOnly From,
  DateOnly To,
  IReadOnlyList<RequestedSpotGroup> RequestedSpots,
  string? Note,
  Guid? GroupReservationId,
  string? GroupReservationSecret,
  string? Language);
