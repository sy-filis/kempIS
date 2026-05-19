using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Operations.MaintenanceIssues;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Operations;

internal sealed class MaintenanceIssuesEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("maintenance-issues")
      .WithTags(Tags.Operations)
      .HasRole(Roles.Receptionist, Roles.CleaningStaff, Roles.Manager);

    group.MapGet(string.Empty, async (
      MaintenanceIssueStatus? status, Guid? spotId, DateTime? from, DateTime? to,
      IQueryHandler<GetMaintenanceIssuesQuery, IReadOnlyList<MaintenanceIssueResponse>> handler,
      CancellationToken cancellationToken) =>
    {
      GetMaintenanceIssuesQuery q = new(status ?? MaintenanceIssueStatus.All, spotId, from, to);
      Result<IReadOnlyList<MaintenanceIssueResponse>> result = await handler.Handle(q, cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("ListMaintenanceIssues")
    .WithSummary("List maintenance issues")
    .WithDescription("""
      Returns maintenance issues newest-first. Optional query parameters narrow the result:
      by status (`Open`, `Resolved`, `All` - defaults to `All`), by spot id, and by an
      issued-at UTC range (`from` and `to` are both inclusive).
      """)
    .Produces<IReadOnlyList<MaintenanceIssueResponse>>(StatusCodes.Status200OK);

    group.MapGet("{id:guid}", async (
      Guid id,
      IQueryHandler<GetMaintenanceIssueQuery, MaintenanceIssueResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<MaintenanceIssueResponse> result = await handler.Handle(new GetMaintenanceIssueQuery(id), cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetMaintenanceIssue")
    .WithSummary("Get a single maintenance issue")
    .WithDescription("""
      Returns the maintenance issue identified by the supplied id, including its target
      spot, problem description, optional solver and resolution timestamp, and free-form
      note.

      **Errors:** `404` no maintenance issue exists with the supplied id.
      """)
    .Produces<MaintenanceIssueResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound);

    group.MapPost(string.Empty, async (
      CreateMaintenanceIssueRequest request,
      ICommandHandler<CreateMaintenanceIssueCommand, Guid> handler,
      CancellationToken cancellationToken) =>
    {
      CreateMaintenanceIssueCommand cmd = new(request.SpotId, request.ProblemDescription, request.Note);
      Result<Guid> result = await handler.Handle(cmd, cancellationToken);
      return result.Match(
        newId => Results.Created($"/maintenance-issues/{newId}", newId),
        CustomResults.Problem);
    })
    .WithName("CreateMaintenanceIssue")
    .WithSummary("Create a maintenance issue")
    .WithDescription("""
      Records a new maintenance issue, optionally pinned to a specific spot. The issued-at
      timestamp is stamped server-side at the current UTC time.

      **Behavior:** problem description is required and capped at 2000 characters; note is
      optional and capped at 2000 characters.

      **Errors:** `400` validation failure (empty problem description, problem description
      or note exceeding 2000 characters).
      """)
    .Produces<Guid>(StatusCodes.Status201Created)
    .ProducesValidationProblem();

    group.MapPatch("{id:guid}", async (
      Guid id, UpdateMaintenanceIssueRequest request,
      ICommandHandler<UpdateMaintenanceIssueCommand> handler,
      CancellationToken cancellationToken) =>
    {
      UpdateMaintenanceIssueCommand cmd = new(id, request.ProblemDescription, request.SolverUserId, request.Note);
      Result result = await handler.Handle(cmd, cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("UpdateMaintenanceIssue")
    .WithSummary("Update a maintenance issue")
    .WithDescription("""
      Replaces the problem description, solver assignment, and note of an existing
      maintenance issue.

      **Behavior:** problem description is required and capped at 2000 characters; note is
      optional and capped at 2000 characters. Setting `SolverUserId` to null unassigns the
      issue.

      **Errors:** `400` validation failure (empty id, empty problem description, problem
      description or note exceeding 2000 characters). `404` no maintenance issue exists
      with the supplied id.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound);

    group.MapPost("{id:guid}/resolve", async (
      Guid id,
      ICommandHandler<ResolveMaintenanceIssueCommand> handler,
      CancellationToken cancellationToken) =>
    {
      Result result = await handler.Handle(new ResolveMaintenanceIssueCommand(id), cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("ResolveMaintenanceIssue")
    .WithSummary("Mark a maintenance issue as resolved")
    .WithDescription("""
      Closes a maintenance issue by stamping the current UTC time as its resolution
      timestamp.

      **Behavior:** the issue must not already be resolved - re-resolving a closed issue
      is rejected.

      **Errors:** `404` no maintenance issue exists with the supplied id. `409` the issue
      has already been resolved.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.CleaningStaff, Roles.Manager);

    group.MapDelete("{id:guid}", async (
      Guid id,
      ICommandHandler<DeleteMaintenanceIssueCommand> handler,
      CancellationToken cancellationToken) =>
    {
      Result result = await handler.Handle(new DeleteMaintenanceIssueCommand(id), cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("DeleteMaintenanceIssue")
    .WithSummary("Delete a maintenance issue")
    .WithDescription("""
      Removes the maintenance issue with the supplied id.

      **Errors:** `404` no maintenance issue exists with the supplied id.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);
  }
}

internal sealed record CreateMaintenanceIssueRequest(Guid? SpotId, string ProblemDescription, string? Note);
internal sealed record UpdateMaintenanceIssueRequest(string ProblemDescription, Guid? SolverUserId, string? Note);
