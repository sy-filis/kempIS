using Application.Abstractions.Authentication;
using Application.Abstractions.EDoklady;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.EDoklady;

internal sealed class GetVirtualServiceCounterEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("edoklady/virtual-service-counters/{id}", async (
        string id,
        IEDokladyClient client,
        CancellationToken cancellationToken) =>
    {
      Result<VirtualServiceCounter> result = await client.GetVirtualServiceCounterAsync(id, cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.EDoklady)
    .WithName("GetEDokladyVirtualServiceCounter")
    .WithSummary("Get an eDoklady virtual service counter")
    .WithDescription("""
      Fetches a virtual service counter from the Czech eDoklady fiscal API by id and
      returns the upstream payload to the caller.

      **Behavior:** when the QR code is close to its expiry (default within 7 days,
      configurable per deployment), a regeneration call is issued upstream and the
      refreshed QR is substituted into the response before returning to the caller.

      **Side effects:** issues an authenticated request (and optionally a QR-code
      regeneration request) to the upstream eDoklady fiscal API.

      **Errors:** `400` upstream rejected the request. `404` no virtual service
      counter exists with the supplied id. `500` the eDoklady service is unreachable
      or returned an unexpected response.
      """)
    .Produces<VirtualServiceCounter>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
