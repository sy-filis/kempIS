using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.LegalEntities.Queries.GetLegalEntityFromAres;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class GetLegalEntityFromAresEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("legal-entities/ares/{cin}", async (
      string cin,
      IQueryHandler<GetLegalEntityFromAresQuery, LegalEntityFinderResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<LegalEntityFinderResponse> result =
        await handler.Handle(new GetLegalEntityFromAresQuery(cin), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Finance)
    .WithName("GetLegalEntityFromAres")
    .WithSummary("Look up a legal entity in the Czech ARES register by CIN")
    .WithDescription("""
      Proxy to the Czech ARES economic-subject registry. Given an 8-digit CIN (IČO), returns the
      legal entity's trade name, tax ID (DIČ), and registered address pre-formatted for use in
      bill and invoice payloads.

      **Behavior:** the CIN must be exactly 8 digits; non-matching values are rejected before any
      upstream call. ARES `404` and `400` responses are translated to a `404` from this endpoint.

      **Errors:** `400` `cin` is missing or not an 8-digit string. `404` no legal entity with the
      given CIN exists in ARES. `500` ARES is unreachable, returned a non-success status, or
      replied with malformed JSON.
      """)
    .Produces<LegalEntityFinderResponse>(StatusCodes.Status200OK)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
