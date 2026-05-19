using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.Nationalities;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class NationalitiesEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("nationalities")
      .WithTags(Tags.Reservations)
      .RequireAuthorization();

    group.MapGet(string.Empty, async (
      IQueryHandler<GetNationalitiesQuery, List<NationalityResponse>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<List<NationalityResponse>> result = await handler.Handle(new GetNationalitiesQuery(), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetNationalities")
    .WithSummary("List nationalities")
    .WithDescription("""
      Returns the seeded nationality catalogue used by guest registration: Czech and English
      names, ISO codes, visa requirements, EU flag, and the matching language code.
      Available to any authenticated user - there is no role restriction.
      """)
    .Produces<List<NationalityResponse>>(StatusCodes.Status200OK);

    group.MapPost(string.Empty, async (
      NationalityRequest request,
      ICommandHandler<CreateNationalityCommand, Guid> handler,
      CancellationToken cancellationToken) =>
    {
      CreateNationalityCommand command = new(
        request.Name,
        request.NameEn,
        request.Alpha2,
        request.Alpha3,
        request.Numeric,
        request.VisaRequired,
        request.BiometricsRequired,
        request.IsEu,
        request.LanguageId);

      Result<Guid> result = await handler.Handle(command, cancellationToken);

      return result.Match(
        id => Results.Created($"/nationalities/{id}", id),
        CustomResults.Problem);
    })
    .WithName("CreateNationality")
    .WithSummary("Create a nationality")
    .WithDescription("""
      Adds a new nationality to the catalogue. Alpha2, Alpha3, and numeric codes
      must each be unique across all nationalities.

      **Behavior:** name and English name are required and capped at 100 characters;
      Alpha2 must be exactly 2 characters; Alpha3 and numeric must be exactly 3;
      languageId must reference an existing language.

      **Errors:** `400` validation failure (empty/over-length fields, wrong-length codes,
      empty languageId). `404` no language exists with the supplied languageId.
      `409` a nationality with the supplied Alpha2, Alpha3, or numeric code already exists.
      """)
    .Produces<Guid>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Manager);

    group.MapPut("{id:guid}", async (
      Guid id,
      NationalityRequest request,
      ICommandHandler<UpdateNationalityCommand> handler,
      CancellationToken cancellationToken) =>
    {
      UpdateNationalityCommand command = new(
        id,
        request.Name,
        request.NameEn,
        request.Alpha2,
        request.Alpha3,
        request.Numeric,
        request.VisaRequired,
        request.BiometricsRequired,
        request.IsEu,
        request.LanguageId);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("UpdateNationality")
    .WithSummary("Update a nationality")
    .WithDescription("""
      Replaces every editable field of an existing nationality. The new Alpha2, Alpha3,
      and numeric codes must remain unique across all other nationalities.

      **Behavior:** name and English name are required and capped at 100 characters;
      Alpha2 must be exactly 2 characters; Alpha3 and numeric must be exactly 3;
      languageId must reference an existing language.

      **Errors:** `400` validation failure. `404` no nationality exists with the supplied
      id, or no language exists with the supplied languageId. `409` another nationality
      already uses the supplied Alpha2, Alpha3, or numeric code.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Manager);

    group.MapDelete("{id:guid}", async (
      Guid id,
      ICommandHandler<DeleteNationalityCommand> handler,
      CancellationToken cancellationToken) =>
    {
      DeleteNationalityCommand command = new(id);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("DeleteNationality")
    .WithSummary("Delete a nationality")
    .WithDescription("""
      Removes the nationality with the supplied id. The nationality must not be
      referenced by any guest.

      **Errors:** `400` the supplied id is empty. `404` no nationality exists with
      the supplied id. `409` the nationality is referenced by one or more guests
      and cannot be deleted.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Manager);
  }
}

internal sealed record NationalityRequest(
  string Name,
  string NameEn,
  string Alpha2,
  string Alpha3,
  string Numeric,
  bool VisaRequired,
  bool BiometricsRequired,
  bool IsEu,
  Guid LanguageId);
