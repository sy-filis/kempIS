using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Services.ServiceTypes;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Services;

internal sealed class ServiceTypesEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("service-types")
      .WithTags(Tags.Services)
      .RequireAuthorization();

    group.MapGet(string.Empty, async (
      IQueryHandler<GetServiceTypesQuery, List<ServiceTypeResponse>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<List<ServiceTypeResponse>> result = await handler.Handle(new GetServiceTypesQuery(), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetServiceTypes")
    .WithSummary("List service types")
    .WithDescription("""
      Returns every service type - the lookup that classifies services as
      accommodation, food, etc. Used by editor screens to assign a service to a
      type.
      """)
    .Produces<List<ServiceTypeResponse>>(StatusCodes.Status200OK);

    group.MapPost(string.Empty, async (
      ServiceTypeRequest request,
      ICommandHandler<CreateServiceTypeCommand, Guid> handler,
      CancellationToken cancellationToken) =>
    {
      CreateServiceTypeCommand command = new(request.Name, request.IsActive);

      Result<Guid> result = await handler.Handle(command, cancellationToken);

      return result.Match(
        id => Results.Created($"/service-types/{id}", id),
        CustomResults.Problem);
    })
    .WithName("CreateServiceType")
    .WithSummary("Create a service type")
    .WithDescription("""
      Adds a new service type to the lookup.

      **Behavior:** name is required and capped at 255 characters.

      **Errors:** `400` validation failure (empty name or name too long).
      """)
    .Produces<Guid>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .HasRole(Roles.Manager);

    group.MapPut("{id:guid}", async (
      Guid id,
      ServiceTypeRequest request,
      ICommandHandler<UpdateServiceTypeCommand> handler,
      CancellationToken cancellationToken) =>
    {
      UpdateServiceTypeCommand command = new(id, request.Name, request.IsActive);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("UpdateServiceType")
    .WithSummary("Update a service type")
    .WithDescription("""
      Replaces the stored name and active flag of an existing service type.

      **Behavior:** name is required and capped at 255 characters.

      **Errors:** `400` validation failure (empty name or name too long). `404` no
      service type exists with the supplied id.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);

    group.MapDelete("{id:guid}", async (
      Guid id,
      ICommandHandler<DeleteServiceTypeCommand> handler,
      CancellationToken cancellationToken) =>
    {
      DeleteServiceTypeCommand command = new(id);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("DeleteServiceType")
    .WithSummary("Delete a service type")
    .WithDescription("""
      Removes the service type with the supplied id.

      **Errors:** `400` the supplied id is empty. `404` no service type exists
      with the supplied id.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);
  }
}

internal sealed record ServiceTypeRequest(string Name, bool IsActive);
