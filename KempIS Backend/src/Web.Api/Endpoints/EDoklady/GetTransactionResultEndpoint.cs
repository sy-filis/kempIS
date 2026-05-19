using Application.Abstractions.Authentication;
using Application.Abstractions.EDoklady;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.EDoklady;

internal sealed class GetTransactionResultEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("edoklady/presentations/{transactionId}/result", async (
        string transactionId,
        IEDokladyClient client,
        CancellationToken cancellationToken,
        [FromQuery] bool includeMDoc = false,
        [FromQuery] bool includeMissingCredentials = false) =>
    {
      Result<TransactionResult> result = await client.GetTransactionResultAsync(
          transactionId, includeMDoc, includeMissingCredentials, cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.EDoklady)
    .WithName("GetEDokladyTransactionResult")
    .WithSummary("Get the result of an eDoklady transaction")
    .WithDescription("""
      Returns the final result of an eDoklady server-flow presentation transaction,
      including the overall outcome (e.g. `Success`, `Untrusted`, `MissingData`,
      `Expired`, `UnknownError`) and the obtained documents with their attributes.

      **Behavior:** the optional `includeMDoc` query flag asks the upstream to return
      the raw mDoc payload alongside parsed attributes. The optional
      `includeMissingCredentials` flag asks the upstream to enumerate attributes that
      were requested but not presented. Both default to `false`.

      **Side effects:** issues an authenticated request to the upstream eDoklady
      fiscal API.

      **Errors:** `400` upstream rejected the request. `404` no transaction exists
      with the supplied id. `500` the eDoklady service is unreachable or returned an
      unexpected response.
      """)
    .Produces<TransactionResult>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
