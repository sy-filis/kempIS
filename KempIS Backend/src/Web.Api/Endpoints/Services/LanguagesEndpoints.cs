using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Services.Languages;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Services;

internal sealed class LanguagesEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("languages")
      .WithTags(Tags.Services)
      .RequireAuthorization();

    group.MapGet(string.Empty, async (
      IQueryHandler<GetLanguagesQuery, List<LanguageResponse>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<List<LanguageResponse>> result = await handler.Handle(new GetLanguagesQuery(), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetLanguages")
    .WithSummary("List languages")
    .WithDescription("""
      Returns every language registered in the system. Languages are referenced by
      service texts and by guest nationalities.
      """)
    .Produces<List<LanguageResponse>>(StatusCodes.Status200OK);

    group.MapPost(string.Empty, async (
      LanguageRequest request,
      ICommandHandler<CreateLanguageCommand, Guid> handler,
      CancellationToken cancellationToken) =>
    {
      CreateLanguageCommand command = new(request.Code, request.Name);

      Result<Guid> result = await handler.Handle(command, cancellationToken);

      return result.Match(
        id => Results.Created($"/languages/{id}", id),
        CustomResults.Problem);
    })
    .WithName("CreateLanguage")
    .WithSummary("Create a language")
    .WithDescription("""
      Adds a new language to the lookup. Codes must be unique across all languages.

      **Behavior:** code is required and capped at 10 characters; name is required
      and capped at 100 characters.

      **Errors:** `400` validation failure (empty code/name, code/name too long).
      `409` a language with the supplied code already exists.
      """)
    .Produces<Guid>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Manager);

    group.MapPut("{id:guid}", async (
      Guid id,
      LanguageRequest request,
      ICommandHandler<UpdateLanguageCommand> handler,
      CancellationToken cancellationToken) =>
    {
      UpdateLanguageCommand command = new(id, request.Code, request.Name);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("UpdateLanguage")
    .WithSummary("Update a language")
    .WithDescription("""
      Replaces the stored code and name of an existing language. The new code
      must remain unique across all other languages.

      **Behavior:** code is required and capped at 10 characters; name is required
      and capped at 100 characters.

      **Errors:** `400` validation failure (empty code/name, code/name too long).
      `404` no language exists with the supplied id. `409` another language
      already uses the supplied code.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Manager);

    group.MapDelete("{id:guid}", async (
      Guid id,
      ICommandHandler<DeleteLanguageCommand> handler,
      CancellationToken cancellationToken) =>
    {
      DeleteLanguageCommand command = new(id);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("DeleteLanguage")
    .WithSummary("Delete a language")
    .WithDescription("""
      Removes the language with the supplied id. The language must not be
      referenced by any nationality.

      **Errors:** `400` the supplied id is empty. `404` no language exists with
      the supplied id. `409` the language is referenced by one or more
      nationalities and cannot be deleted.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Manager);
  }
}

internal sealed record LanguageRequest(string Code, string Name);
