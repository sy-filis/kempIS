using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Operations.OutOfOrders;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Operations;

internal sealed class OutOfOrdersEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("out-of-orders")
      .WithTags(Tags.Operations)
      .HasRole(Roles.Receptionist, Roles.CleaningStaff, Roles.Manager);

    group.MapGet(string.Empty, async (
      DateOnly? from,
      DateOnly? to,
      IQueryHandler<GetOutOfOrdersQuery, List<OutOfOrderResponse>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<List<OutOfOrderResponse>> result = await handler.Handle(new GetOutOfOrdersQuery(from, to), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetOutOfOrders")
    .WithSummary("List out-of-order blocks, optionally filtered by overlap with a date range")
    .WithDescription("""
      Returns out-of-order (OOO) blocks currently stored, including the targeted spot
      groups and individual spots. Used by reception and cleaning staff to see which spots
      are unavailable due to maintenance, repairs, or other operational reasons.

      **Behavior:** when `from` and/or `to` query parameters are supplied (ISO `yyyy-MM-dd`
      dates), only blocks that overlap with the supplied range are returned. A block
      overlaps when its `[From, To]` date range intersects `[from, to]` (inclusive on both
      ends). Either bound can be omitted to leave that side open. With neither bound
      supplied, all blocks are returned. Each entry exposes the date range, reason, and
      the IDs of the affected spot groups and spots.
      """)
    .Produces<List<OutOfOrderResponse>>(StatusCodes.Status200OK);

    group.MapPost(string.Empty, async (
      OutOfOrderRequest request,
      ICommandHandler<CreateOutOfOrderCommand, Guid> handler,
      CancellationToken cancellationToken) =>
    {
      CreateOutOfOrderCommand command = new(
        request.From,
        request.To,
        request.Reason,
        request.SpotGroupIds,
        request.SpotIds);

      Result<Guid> result = await handler.Handle(command, cancellationToken);

      return result.Match(
        id => Results.Created($"/out-of-orders/{id}", id),
        CustomResults.Problem);
    })
    .WithName("CreateOutOfOrder")
    .WithSummary("Create an out-of-order block for spots and/or spot groups")
    .WithDescription("""
      Creates an out-of-order (OOO) block over the supplied date range, marking the
      selected spots and/or spot groups as unavailable for the given reason.

      **Behavior:** at least one spot group or spot must be supplied. `To` must be on or
      after `From` (same-day blocks are allowed). Spot and spot-group ID lists must each
      be free of duplicates. The reason is required and capped at 1000 characters.

      **Errors:** `400` validation failure (empty reason, range inverted, both ID lists
      empty, duplicates in either list).
      """)
    .Produces<Guid>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .HasRole(Roles.Receptionist, Roles.Manager);

    group.MapPut("{id:guid}", async (
      Guid id,
      OutOfOrderRequest request,
      ICommandHandler<UpdateOutOfOrderCommand> handler,
      CancellationToken cancellationToken) =>
    {
      UpdateOutOfOrderCommand command = new(
        id,
        request.From,
        request.To,
        request.Reason,
        request.SpotGroupIds,
        request.SpotIds);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("UpdateOutOfOrder")
    .WithSummary("Update an out-of-order block")
    .WithDescription("""
      Replaces the stored date range, reason, and target spots/spot groups of an existing
      out-of-order block. The set of items is rewritten - items present in the request are
      kept or added, and items not present are removed.

      **Behavior:** at least one spot group or spot must be supplied. `To` must be on or
      after `From` (same-day blocks are allowed). Spot and spot-group ID lists must each
      be free of duplicates. The reason is required and capped at 1000 characters.

      **Errors:** `400` validation failure (empty reason, range inverted, both ID lists
      empty, duplicates in either list). `404` no OOO block exists with the supplied id.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager);

    group.MapDelete("{id:guid}", async (
      Guid id,
      ICommandHandler<DeleteOutOfOrderCommand> handler,
      CancellationToken cancellationToken) =>
    {
      DeleteOutOfOrderCommand command = new(id);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("DeleteOutOfOrder")
    .WithSummary("Delete an out-of-order block")
    .WithDescription("""
      Removes the out-of-order block with the supplied id, freeing the associated spots
      and spot groups. Cascade delete cleans up the linked spot and spot-group items.

      **Errors:** `400` the supplied id is empty. `404` no OOO block exists with the
      supplied id.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}

internal sealed record OutOfOrderRequest(
  DateOnly From,
  DateOnly To,
  string Reason,
  IReadOnlyList<Guid> SpotGroupIds,
  IReadOnlyList<Guid> SpotIds);
