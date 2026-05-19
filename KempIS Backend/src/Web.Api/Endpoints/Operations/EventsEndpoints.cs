using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Operations.Events;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Operations;

internal sealed class EventsEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("events")
      .WithTags(Tags.Operations)
      .RequireAuthorization();

    group.MapGet(string.Empty, async (
      IQueryHandler<GetEventsQuery, List<EventResponse>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<List<EventResponse>> result = await handler.Handle(new GetEventsQuery(), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetEvents")
    .WithSummary("List events")
    .WithDescription("""
      Returns every event in the system. Used by reception and managers to see scheduled
      and historical events, along with the spot groups each event covers.

      **Behavior:** no filtering is applied - past, current, and future events are all
      returned. Each entry exposes the name, optional description, start date, optional
      end date, and the IDs of the affected spot groups.
      """)
    .Produces<List<EventResponse>>(StatusCodes.Status200OK);

    group.MapPost(string.Empty, async (
      EventRequest request,
      ICommandHandler<CreateEventCommand, Guid> handler,
      CancellationToken cancellationToken) =>
    {
      CreateEventCommand command = new(
        request.Name,
        request.Description,
        request.StartsAt,
        request.EndsAt,
        request.SpotGroupIds);

      Result<Guid> result = await handler.Handle(command, cancellationToken);

      return result.Match(
        id => Results.Created($"/events/{id}", id),
        CustomResults.Problem);
    })
    .WithName("CreateEvent")
    .WithSummary("Create an event")
    .WithDescription("""
      Creates an event covering one or more spot groups. Events are informational markers
      used to communicate organized activities (concerts, workshops, etc.) to staff.

      **Behavior:** name is required and capped at 255 characters; description is optional
      and capped at 1000 characters. `EndsAt` is optional but, when supplied, must be on
      or after `StartsAt`. At least one spot group must be supplied and the list must be
      free of duplicates.

      **Errors:** `400` validation failure (empty name, name/description too long, end
      before start, no spot groups, duplicate spot-group ids).
      """)
    .Produces<Guid>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .HasRole(Roles.Manager);

    group.MapPut("{id:guid}", async (
      Guid id,
      EventRequest request,
      ICommandHandler<UpdateEventCommand> handler,
      CancellationToken cancellationToken) =>
    {
      UpdateEventCommand command = new(
        id,
        request.Name,
        request.Description,
        request.StartsAt,
        request.EndsAt,
        request.SpotGroupIds);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("UpdateEvent")
    .WithSummary("Update an event")
    .WithDescription("""
      Replaces the stored name, description, time range, and target spot groups of an
      existing event. The set of spot groups is rewritten - items present in the request
      are kept or added, and items not present are removed.

      **Behavior:** name is required and capped at 255 characters; description is optional
      and capped at 1000 characters. `EndsAt` is optional but, when supplied, must be on
      or after `StartsAt`. At least one spot group must be supplied and the list must be
      free of duplicates.

      **Errors:** `400` validation failure (empty name, name/description too long, end
      before start, no spot groups, duplicate spot-group ids). `404` no event exists with
      the supplied id.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);

    group.MapDelete("{id:guid}", async (
      Guid id,
      ICommandHandler<DeleteEventCommand> handler,
      CancellationToken cancellationToken) =>
    {
      DeleteEventCommand command = new(id);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("DeleteEvent")
    .WithSummary("Delete an event")
    .WithDescription("""
      Removes the event with the supplied id. Cascade delete cleans up the linked spot-group
      items.

      **Errors:** `400` the supplied id is empty. `404` no event exists with the supplied id.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);
  }
}

internal sealed record EventRequest(
  string Name,
  string? Description,
  DateOnly StartsAt,
  DateOnly? EndsAt,
  IReadOnlyList<Guid> SpotGroupIds);
