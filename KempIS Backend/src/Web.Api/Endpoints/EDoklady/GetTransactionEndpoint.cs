using Application.Abstractions.Authentication;
using Application.Abstractions.EDoklady;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.EDoklady;

internal sealed class GetTransactionEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("edoklady/presentations/{transactionId}", async (
        string transactionId,
        IEDokladyClient client,
        CancellationToken cancellationToken) =>
    {
      Result<TransactionState> result = await client.GetTransactionAsync(transactionId, cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.EDoklady)
    .WithName("GetEDokladyTransaction")
    .WithSummary("Get an eDoklady transaction")
    .WithDescription("""
      Returns the current state of an eDoklady server-flow presentation transaction -
      its id, lifecycle state (e.g. `Open`, `WaitingForResponse`, `ResponseReceived`,
      `Finished`, `Canceled`, `Failed`, `Timeout`), and validity timestamp. Intended
      for polling while a presentation is in-flight.

      **Side effects:** issues an authenticated request to the upstream eDoklady
      fiscal API.

      **Errors:** `400` upstream rejected the request. `404` no transaction exists
      with the supplied id. `500` the eDoklady service is unreachable or returned an
      unexpected response.
      """)
    .Produces<TransactionState>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
