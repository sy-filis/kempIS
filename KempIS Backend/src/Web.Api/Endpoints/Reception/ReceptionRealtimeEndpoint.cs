using System.Net.WebSockets;
using Web.Api.Realtime;

namespace Web.Api.Endpoints.Reception;

internal sealed class ReceptionRealtimeEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    // Anonymous: the pair code is the sole capability; the tablet PWA cannot present credentials.
    app.MapGet("reception/realtime", async (
      HttpContext context,
      ReceptionRealtimeSession session,
      CancellationToken cancellationToken) =>
    {
      if (!context.WebSockets.IsWebSocketRequest)
      {
        return Results.BadRequest("WebSocket upgrade required.");
      }

      using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
      await session.RunAsync(socket, cancellationToken);
      return Results.Empty;
    })
    .WithTags(Tags.Reception)
    .WithName("ReceptionRealtime")
    .ExcludeFromDescription();
  }
}
