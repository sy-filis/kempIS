using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Services.ServiceTexts;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Services;

internal sealed class ServiceTextsEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("service-texts")
      .WithTags(Tags.Services)
      .RequireAuthorization();

    group.MapGet(string.Empty, async (
      IQueryHandler<GetServiceTextsQuery, List<ServiceTextResponse>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<List<ServiceTextResponse>> result = await handler.Handle(new GetServiceTextsQuery(), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetServiceTexts")
    .WithSummary("List service texts")
    .WithDescription("""
      Returns every per-language print text registered for a service. These texts
      drive what is printed on bills and other documents in the guest's language.
      """)
    .Produces<List<ServiceTextResponse>>(StatusCodes.Status200OK);

    group.MapPost(string.Empty, async (
      ServiceTextRequest request,
      ICommandHandler<CreateServiceTextCommand, Guid> handler,
      CancellationToken cancellationToken) =>
    {
      CreateServiceTextCommand command = new(request.ServiceId, request.LanguageId, request.PrintText);

      Result<Guid> result = await handler.Handle(command, cancellationToken);

      return result.Match(
        id => Results.Created($"/service-texts/{id}", id),
        CustomResults.Problem);
    })
    .WithName("CreateServiceText")
    .WithSummary("Create a service text")
    .WithDescription("""
      Adds a new print text for the given service and language combination. Each
      service may have at most one text per language.

      **Behavior:** print text is required and capped at 1000 characters. The
      referenced service and language must already exist.

      **Errors:** `400` validation failure (empty ids/text, text too long). `404`
      the referenced service or language does not exist. `409` a text already
      exists for the given service and language.
      """)
    .Produces<Guid>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Manager);

    group.MapPut("{id:guid}", async (
      Guid id,
      ServiceTextRequest request,
      ICommandHandler<UpdateServiceTextCommand> handler,
      CancellationToken cancellationToken) =>
    {
      UpdateServiceTextCommand command = new(id, request.ServiceId, request.LanguageId, request.PrintText);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("UpdateServiceText")
    .WithSummary("Update a service text")
    .WithDescription("""
      Replaces the stored service, language, and print text of an existing service
      text. The uniqueness rule (one text per service/language) is re-checked
      against the new combination.

      **Behavior:** print text is required and capped at 1000 characters. The
      referenced service and language must already exist.

      **Errors:** `400` validation failure (empty ids/text, text too long). `404`
      no service text exists with the supplied id, or the referenced service or
      language does not exist. `409` another text already exists for the given
      service and language.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Manager);

    group.MapDelete("{id:guid}", async (
      Guid id,
      ICommandHandler<DeleteServiceTextCommand> handler,
      CancellationToken cancellationToken) =>
    {
      DeleteServiceTextCommand command = new(id);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("DeleteServiceText")
    .WithSummary("Delete a service text")
    .WithDescription("""
      Removes the service text with the supplied id.

      **Errors:** `400` the supplied id is empty. `404` no service text exists
      with the supplied id.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Manager);
  }
}

internal sealed record ServiceTextRequest(Guid ServiceId, Guid LanguageId, string PrintText);
