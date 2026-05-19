using Application.Abstractions.Authentication;
using Application.Abstractions.EDoklady;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.EDoklady;

internal sealed class CreateVirtualServiceCounterEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("edoklady/virtual-service-counters", async (
        [FromBody] CreateVirtualServiceCounterRequest request,
        IEDokladyClient client,
        CancellationToken cancellationToken) =>
    {
      Result<VirtualServiceCounter> result = await client.CreateVirtualServiceCounterAsync(request, cancellationToken);
      return result.Match(v => Results.Created($"/edoklady/virtual-service-counters/{v.Id}", v), CustomResults.Problem);
    })
    .WithTags(Tags.EDoklady)
    .WithName("CreateEDokladyVirtualServiceCounter")
    .WithSummary("Create an eDoklady virtual service counter")
    .WithDescription("""
      Provisions a new virtual service counter on the Czech eDoklady fiscal API and
      returns the upstream payload - including the freshly issued QR code - to the
      caller.

      **Behavior:** the optional `Name` is forwarded as-is. When the deployment is
      configured with a reception location, its geolocation (latitude, longitude,
      tolerance) is attached to the request.

      **Side effects:** issues an authenticated request to the upstream eDoklady fiscal
      API.

      **Errors:** `400` upstream rejected the request (e.g. malformed payload).
      `500` the eDoklady service is unreachable or returned an unexpected response.
      """)
    .Produces<VirtualServiceCounter>(StatusCodes.Status201Created)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status500InternalServerError)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
