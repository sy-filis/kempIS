using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Addresses.Queries.SuggestAddresses;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Addresses;

internal sealed class SuggestAddressesEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("addresses/whisperer", async (
      [FromQuery] string query,
      IQueryHandler<SuggestAddressesQuery, IReadOnlyList<AddressSuggestion>> handler,
      CancellationToken cancellationToken,
      [FromQuery] bool foreign = false,
      [FromQuery] int? limit = null) =>
    {
      int effectiveLimit = Math.Min(limit ?? 5, 5);

      Result<IReadOnlyList<AddressSuggestion>> result =
        await handler.Handle(new SuggestAddressesQuery(query, foreign, effectiveLimit), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Addresses)
    .WithName("SuggestAddresses")
    .WithSummary("Suggest addresses by free-text query")
    .WithDescription("""
      Returns up to five address suggestions for a free-text query, sourced from
      external Czech address whisperer services and cached for repeat queries.

      **Behavior:** the trimmed query must be longer than 8 characters. `limit`
      defaults to 5 and is clamped server-side to a maximum of 5. When `foreign` is
      `false` (default), the RUIAN provider is queried first and falls back to the
      Mapy.cz provider when RUIAN returns no rows; when `foreign` is `true`, only
      Mapy.cz is queried. Successful results are cached for 180 days; empty results
      for 1 day. The query is normalized (lowercased, whitespace collapsed) before
      caching.

      **Side effects:** proxies to RUIAN and/or Mapy.cz; populates the suggestion
      cache.

      **Errors:** `400` validation failure (trimmed query is 8 characters or shorter,
      or effective limit is below 1). `500` both upstream providers are unreachable
      or returned an error.
      """)
    .Produces<IReadOnlyList<AddressSuggestion>>(StatusCodes.Status200OK)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
