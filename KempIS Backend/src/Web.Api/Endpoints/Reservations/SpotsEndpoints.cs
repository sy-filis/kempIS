using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.Spots;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class SpotsEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("spots")
      .WithTags(Tags.Reservations)
      .RequireAuthorization();

    group.MapGet(string.Empty, async (
      IQueryHandler<GetSpotsQuery, List<SpotResponse>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<List<SpotResponse>> result = await handler.Handle(new GetSpotsQuery(), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetSpots")
    .WithSummary("List spots")
    .WithDescription("""
      Returns every spot in the system regardless of its active flag. Available to any
      authenticated user.
      """)
    .Produces<List<SpotResponse>>(StatusCodes.Status200OK);

    group.MapGet("states", async (
      IQueryHandler<GetSpotStatesQuery, List<SpotStateResponse>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<List<SpotStateResponse>> result = await handler.Handle(new GetSpotStatesQuery(), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetSpotStates")
    .WithSummary("Get derived state for each spot")
    .WithDescription("""
      Returns the live state for every active spot, derived from today's reservations and
      out-of-orders. Possible states: `OutOfOrder`, `Unoccupied`, `ExpectingArrival` (a
      confirmed reservation starts today), `Occupied` (checked-in, departure later), or
      `ExpectingDeparture` (checked-in, departure today). For occupied/expecting-departure
      spots the response includes the reservation's departure date. Spots whose keys have
      already been returned for today's stay are treated as unoccupied.
      """)
    .Produces<List<SpotStateResponse>>(StatusCodes.Status200OK);

    group.MapPost(string.Empty, async (
      SpotRequest request,
      ICommandHandler<CreateSpotCommand, Guid> handler,
      CancellationToken cancellationToken) =>
    {
      CreateSpotCommand command = new(
        request.SpotGroupId,
        request.Name,
        request.Description,
        request.IsActive);

      Result<Guid> result = await handler.Handle(command, cancellationToken);

      return result.Match(
        id => Results.Created($"/spots/{id}", id),
        CustomResults.Problem);
    })
    .WithName("CreateSpot")
    .WithSummary("Create a spot")
    .WithDescription("""
      Adds a new bookable spot to a spot group.

      **Errors:** `400` invalid payload (missing name, name over 255 chars, description over
      1000 chars). `404` the parent spot group does not exist.
      """)
    .Produces<Guid>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);

    group.MapPut("{id:guid}", async (
      Guid id,
      SpotRequest request,
      ICommandHandler<UpdateSpotCommand> handler,
      CancellationToken cancellationToken) =>
    {
      UpdateSpotCommand command = new(
        id,
        request.SpotGroupId,
        request.Name,
        request.Description,
        request.IsActive);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("UpdateSpot")
    .WithSummary("Update a spot")
    .WithDescription("""
      Edits a spot's name, description, active flag, and parent spot group.

      **Errors:** `400` invalid payload. `404` spot does not exist, or the new spot group does
      not exist when the group is being changed.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);

    group.MapDelete("{id:guid}", async (
      Guid id,
      ICommandHandler<DeleteSpotCommand> handler,
      CancellationToken cancellationToken) =>
    {
      DeleteSpotCommand command = new(id);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("DeleteSpot")
    .WithSummary("Delete a spot")
    .WithDescription("""
      Permanently removes a spot. Use the spot's `IsActive` flag instead when the spot has
      historical reservations attached.

      **Errors:** `404` spot does not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);
  }
}

internal sealed record SpotRequest(
  Guid SpotGroupId,
  string Name,
  string? Description,
  bool IsActive);
