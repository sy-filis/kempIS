using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Services.VatRates;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Services;

internal sealed class VatRatesEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("vat-rates")
      .WithTags(Tags.Services)
      .RequireAuthorization();

    group.MapGet(string.Empty, async (
      IQueryHandler<GetVatRatesQuery, List<VatRateResponse>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<List<VatRateResponse>> result = await handler.Handle(new GetVatRatesQuery(), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetVatRates")
    .WithSummary("List VAT rates")
    .WithDescription("""
      Returns every VAT rate registered in the system. VAT rates are referenced by
      services to compute tax on bills.
      """)
    .Produces<List<VatRateResponse>>(StatusCodes.Status200OK);

    group.MapPost(string.Empty, async (
      VatRateRequest request,
      ICommandHandler<CreateVatRateCommand, Guid> handler,
      CancellationToken cancellationToken) =>
    {
      CreateVatRateCommand command = new(request.Name, request.Rate, request.IsActive);

      Result<Guid> result = await handler.Handle(command, cancellationToken);

      return result.Match(
        id => Results.Created($"/vat-rates/{id}", id),
        CustomResults.Problem);
    })
    .WithName("CreateVatRate")
    .WithSummary("Create a VAT rate")
    .WithDescription("""
      Adds a new VAT rate to the lookup.

      **Behavior:** name is required and capped at 100 characters; rate must be
      between 0 and 100 (inclusive).

      **Errors:** `400` validation failure (empty name, name too long, rate out of
      range).
      """)
    .Produces<Guid>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .HasRole(Roles.Manager);

    group.MapPut("{id:guid}", async (
      Guid id,
      VatRateRequest request,
      ICommandHandler<UpdateVatRateCommand> handler,
      CancellationToken cancellationToken) =>
    {
      UpdateVatRateCommand command = new(id, request.Name, request.Rate, request.IsActive);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("UpdateVatRate")
    .WithSummary("Update a VAT rate")
    .WithDescription("""
      Replaces the stored name, rate, and active flag of an existing VAT rate.

      **Behavior:** name is required and capped at 100 characters; rate must be
      between 0 and 100 (inclusive).

      **Errors:** `400` validation failure (empty name, name too long, rate out of
      range). `404` no VAT rate exists with the supplied id.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);

    group.MapDelete("{id:guid}", async (
      Guid id,
      ICommandHandler<DeleteVatRateCommand> handler,
      CancellationToken cancellationToken) =>
    {
      DeleteVatRateCommand command = new(id);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("DeleteVatRate")
    .WithSummary("Delete a VAT rate")
    .WithDescription("""
      Removes the VAT rate with the supplied id.

      **Errors:** `400` the supplied id is empty. `404` no VAT rate exists with
      the supplied id.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);
  }
}

internal sealed record VatRateRequest(string Name, decimal Rate, bool IsActive);
