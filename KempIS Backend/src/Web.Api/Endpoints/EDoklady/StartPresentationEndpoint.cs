using Application.Abstractions.Authentication;
using Application.Abstractions.EDoklady;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.EDoklady;

internal sealed class StartPresentationEndpoint : IEndpoint
{
  public sealed record StartPresentationRequest(string VirtualServiceCounterId);
  public sealed record StartPresentationResponse(string TransactionId);

  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("edoklady/presentations", async (
        [FromBody] StartPresentationRequest request,
        IEDokladyClient client,
        CancellationToken cancellationToken) =>
    {
      if (string.IsNullOrWhiteSpace(request.VirtualServiceCounterId))
      {
        return CustomResults.Problem(
            Result.Failure(Error.Problem("EDoklady.VirtualServiceCounterIdRequired",
              "virtualServiceCounterId is required.")));
      }

      Result<string> result = await client.StartPresentationAsync(request.VirtualServiceCounterId, cancellationToken);
      return result.Match(
          tx => Results.Created($"/edoklady/presentations/{tx}", new StartPresentationResponse(tx)),
          CustomResults.Problem);
    })
    .WithTags(Tags.EDoklady)
    .WithName("StartEDokladyPresentation")
    .WithSummary("Start an eDoklady fiscal presentation for a bill")
    .WithDescription("""
      Initiates a server-flow document presentation against the Czech eDoklady fiscal
      API and returns the upstream `transactionId` to be polled via the transaction
      endpoints.

      **Behavior:** requests the Czech mobile ID document with a fixed attribute set
      covering portrait, names, nationality, document number, expiry date, and
      residence. `VirtualServiceCounterId` is required.

      **Side effects:** issues an authenticated request to the upstream eDoklady
      fiscal API.

      **Errors:** `400` `VirtualServiceCounterId` was missing/blank, or the upstream
      rejected the request. `404` the referenced virtual service counter does not
      exist upstream. `500` the eDoklady service is unreachable or returned an
      unexpected response.
      """)
    .Produces<StartPresentationResponse>(StatusCodes.Status201Created)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
