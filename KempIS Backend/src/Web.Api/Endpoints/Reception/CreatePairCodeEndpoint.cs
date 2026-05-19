using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reception.PairCodes.Commands.CreatePairCode;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reception;

internal sealed class CreatePairCodeEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("reception/pair-codes", async (
        [FromServices] ICommandHandler<CreatePairCodeCommand, CreatePairCodeResponse> handler,
        CancellationToken cancellationToken) =>
    {
      Result<CreatePairCodeResponse> result = await handler.Handle(new CreatePairCodeCommand(), cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Reception)
    .WithName("CreateReceptionPairCode")
    .WithSummary("Issue a single-use pair code for the reception tablet handshake")
    .WithDescription("""
      Returns a 256-bit random pair code and its UTC expiry. The desktop displays the
      code as a QR; the tablet PWA scans it, connects to `GET /api/reception/realtime`
      over WebSocket, and emits `pair:join` using that code. The code is consumed on
      `pair:ready` and cannot be reused.
      """)
    .Produces<CreatePairCodeResponse>(StatusCodes.Status200OK)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
