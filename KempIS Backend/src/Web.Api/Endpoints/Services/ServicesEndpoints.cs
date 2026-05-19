using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Services.Services;
using Domain.Services.Services;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Services;

internal sealed class ServicesEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("services")
      .WithTags(Tags.Services)
      .RequireAuthorization();

    group.MapGet(string.Empty, async (
      IQueryHandler<GetServicesQuery, List<ServiceResponse>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<List<ServiceResponse>> result = await handler.Handle(new GetServicesQuery(), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetServices")
    .WithSummary("List services")
    .WithDescription("""
      Returns every saleable service in the catalogue. Used to populate dropdowns
      on reservations, bills, and other documents.

      **Behavior:** each entry exposes the service group bucket, service type, VAT
      rate, name, base price, and active flag. No filtering is applied.
      """)
    .Produces<List<ServiceResponse>>(StatusCodes.Status200OK);

    group.MapPost(string.Empty, async (
      ServiceRequest request,
      ICommandHandler<CreateServiceCommand, Guid> handler,
      CancellationToken cancellationToken) =>
    {
      CreateServiceCommand command = new(
        request.ServiceGroup,
        request.ServiceTypeId,
        request.VatRateId,
        request.Name,
        request.BasePrice,
        request.IsActive);

      Result<Guid> result = await handler.Handle(command, cancellationToken);

      return result.Match(
        id => Results.Created($"/services/{id}", id),
        CustomResults.Problem);
    })
    .WithName("CreateService")
    .WithSummary("Create a service")
    .WithDescription("""
      Adds a new service to the catalogue.

      **Behavior:** name is required and capped at 255 characters; base price must
      be zero or greater. The referenced service type and VAT rate must already
      exist.

      **Errors:** `400` validation failure (empty name/ids, name too long, negative
      base price, unknown service group). `404` the referenced service type or VAT
      rate does not exist.
      """)
    .Produces<Guid>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);

    group.MapPut("{id:guid}", async (
      Guid id,
      ServiceRequest request,
      ICommandHandler<UpdateServiceCommand> handler,
      CancellationToken cancellationToken) =>
    {
      UpdateServiceCommand command = new(
        id,
        request.ServiceGroup,
        request.ServiceTypeId,
        request.VatRateId,
        request.Name,
        request.BasePrice,
        request.IsActive);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("UpdateService")
    .WithSummary("Update a service")
    .WithDescription("""
      Replaces the stored service group, service type, VAT rate, name, base price,
      and active flag of an existing service.

      **Behavior:** name is required and capped at 255 characters; base price must
      be zero or greater. The referenced service type and VAT rate must already
      exist.

      **Errors:** `400` validation failure (empty name/ids, name too long, negative
      base price, unknown service group). `404` no service exists with the supplied
      id, or the referenced service type or VAT rate does not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);

    group.MapDelete("{id:guid}", async (
      Guid id,
      ICommandHandler<DeleteServiceCommand> handler,
      CancellationToken cancellationToken) =>
    {
      DeleteServiceCommand command = new(id);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("DeleteService")
    .WithSummary("Delete a service")
    .WithDescription("""
      Removes the service with the supplied id from the catalogue.

      **Errors:** `400` the supplied id is empty. `404` no service exists with the
      supplied id.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);
  }
}

internal sealed record ServiceRequest(
  ServiceGroup ServiceGroup,
  Guid ServiceTypeId,
  Guid VatRateId,
  string Name,
  decimal BasePrice,
  bool IsActive);
