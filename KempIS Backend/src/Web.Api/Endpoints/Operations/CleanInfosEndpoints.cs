using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Operations.CleaningPlans;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Operations;

internal sealed class CleanInfosEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("cleaning-plans/{date}/clean-infos", async (
      DateOnly date,
      AddCleanInfoRequest request,
      ICommandHandler<AddCleanInfoCommand, Guid> handler,
      CancellationToken cancellationToken) =>
    {
      Result<Guid> result = await handler.Handle(new AddCleanInfoCommand(date, request.SpotId), cancellationToken);
      return result.Match(
        newId => Results.Created($"/clean-infos/{newId}", newId),
        CustomResults.Problem);
    })
    .WithTags(Tags.Operations)
    .WithName("AddCleanInfo")
    .WithSummary("Add a spot to the date's cleaning plan")
    .WithDescription("""
      Appends a clean-info entry for a spot to the cleaning plan for the supplied date. If
      no plan exists for the date, an empty plan is auto-created before the supplied spot
      is added.

      **Behavior:** the spot must not already be present in the plan - each plan may track
      a given spot only once.

      **Errors:** `409` the spot is already part of this cleaning plan.
      """)
    .Produces<Guid>(StatusCodes.Status201Created)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Receptionist, Roles.Manager);

    app.MapDelete("clean-infos/{id:guid}", async (
      Guid id,
      ICommandHandler<DeleteCleanInfoCommand> handler,
      CancellationToken cancellationToken) =>
    {
      Result result = await handler.Handle(new DeleteCleanInfoCommand(id), cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Operations)
    .WithName("DeleteCleanInfo")
    .WithSummary("Remove a clean-info entry from its cleaning plan")
    .WithDescription("""
      Deletes the clean-info entry with the supplied id, removing the associated spot from
      its cleaning plan.

      **Errors:** `404` no clean-info entry exists with the supplied id.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager);

    app.MapPatch("clean-infos/{id:guid}", async (
      Guid id,
      UpdateCleanInfoRequest request,
      ICommandHandler<UpdateCleanInfoCommand> handler,
      CancellationToken cancellationToken) =>
    {
      Result result = await handler.Handle(
        new UpdateCleanInfoCommand(id, request.Note),
        cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Operations)
    .WithName("UpdateCleanInfo")
    .WithSummary("Patch a clean-info entry's note")
    .WithDescription("""
      Partially updates a clean-info entry. `Note` is updated only when the request supplies
      a non-null string; an omitted or null value leaves the existing note untouched.

      **Errors:** `404` no clean-info entry exists with the supplied id.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager);

    app.MapPost("clean-infos/{id:guid}/mark-cleaned", async (
      Guid id,
      MarkCleanedRequest? request,
      ICommandHandler<MarkCleanInfoCleanedCommand> handler,
      CancellationToken cancellationToken) =>
    {
      Result result = await handler.Handle(new MarkCleanInfoCleanedCommand(id, request?.Note), cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Operations)
    .WithName("MarkCleanInfoCleaned")
    .WithSummary("Mark a clean-info entry as cleaned")
    .WithDescription("""
      Closes a clean-info entry by stamping the current UTC time and the calling user as
      the responsible cleaner. Optionally overwrites the entry's note in the same call.

      **Behavior:** the entry must not already be marked cleaned - re-marking a completed
      entry is rejected. When supplied, `Note` overwrites the existing note.

      **Errors:** `404` no clean-info entry exists with the supplied id. `409` the entry
      has already been marked as cleaned.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.CleaningStaff, Roles.Manager);
  }
}

internal sealed record AddCleanInfoRequest(Guid SpotId);
internal sealed record UpdateCleanInfoRequest(string? Note);
internal sealed record MarkCleanedRequest(string? Note);
