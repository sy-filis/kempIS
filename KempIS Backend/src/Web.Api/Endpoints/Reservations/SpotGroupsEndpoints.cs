using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.SpotGroups;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class SpotGroupsEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("spot-groups")
      .WithTags(Tags.Reservations)
      .RequireAuthorization();

    group.MapGet(string.Empty, async (
      IQueryHandler<GetSpotGroupsQuery, List<SpotGroupResponse>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<List<SpotGroupResponse>> result = await handler.Handle(new GetSpotGroupsQuery(), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetSpotGroups")
    .WithSummary("List spot groups")
    .WithDescription("""
      Returns every spot group with its capacity, active flag, and the public image and details
      URLs used by the booking widget. Available to any authenticated user.
      """)
    .Produces<List<SpotGroupResponse>>(StatusCodes.Status200OK);

    group.MapPost(string.Empty, async (
      SpotGroupRequest request,
      ICommandHandler<CreateSpotGroupCommand, Guid> handler,
      CancellationToken cancellationToken) =>
    {
      CreateSpotGroupCommand command = new(
        request.ServiceId,
        request.Name,
        request.Description,
        request.Capacity,
        request.IsActive,
        request.ImageUrl,
        request.DetailsUrl);

      Result<Guid> result = await handler.Handle(command, cancellationToken);

      return result.Match(
        id => Results.Created($"/spot-groups/{id}", id),
        CustomResults.Problem);
    })
    .WithName("CreateSpotGroup")
    .WithSummary("Create a spot group")
    .WithDescription("""
      Adds a new spot group bound to a service. The group's capacity is the cap on simultaneous
      bookings; `imageUrl` and `detailsUrl` are surfaced by the public booking widget.

      **Errors:** `400` invalid payload (missing name, capacity not greater than zero, missing
      or oversized image/details URL).
      """)
    .Produces<Guid>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .HasRole(Roles.Manager);

    group.MapPut("{id:guid}", async (
      Guid id,
      SpotGroupRequest request,
      ICommandHandler<UpdateSpotGroupCommand> handler,
      CancellationToken cancellationToken) =>
    {
      UpdateSpotGroupCommand command = new(
        id,
        request.ServiceId,
        request.Name,
        request.Description,
        request.Capacity,
        request.IsActive,
        request.ImageUrl,
        request.DetailsUrl);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("UpdateSpotGroup")
    .WithSummary("Update a spot group")
    .WithDescription("""
      Edits a spot group's service binding, name, capacity, active flag, and public image and
      details URLs.

      **Errors:** `400` invalid payload. `404` spot group does not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);

    group.MapDelete("{id:guid}", async (
      Guid id,
      ICommandHandler<DeleteSpotGroupCommand> handler,
      CancellationToken cancellationToken) =>
    {
      DeleteSpotGroupCommand command = new(id);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("DeleteSpotGroup")
    .WithSummary("Delete a spot group")
    .WithDescription("""
      Permanently removes a spot group. Use the `IsActive` flag instead when historical spots
      or reservations reference the group.

      **Errors:** `404` spot group does not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);
  }
}

internal sealed record SpotGroupRequest(
  Guid ServiceId,
  string Name,
  string? Description,
  uint Capacity,
  bool IsActive,
  string ImageUrl,
  string DetailsUrl);
