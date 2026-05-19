using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Application.Reception.Realtime;
using NSubstitute;
using Web.Api.Realtime;

namespace Web.Api.IntegrationTests.Reception;

public sealed class WebSocketRealtimeSocketTests
{
  [Fact]
  public async Task EmitAsync_WritesEnvelopeAsUtf8TextFrame()
  {
    WebSocket ws = Substitute.For<WebSocket>();
    ws.State.Returns(WebSocketState.Open);
    List<byte[]> captured = [];

#pragma warning disable CA2012 // ValueTask returned by NSubstitute setup is handled by the framework, not the caller
    ws.SendAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<WebSocketMessageType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
      .Returns(call =>
      {
        captured.Add(call.Arg<ReadOnlyMemory<byte>>().ToArray());
        return default;
      });
#pragma warning restore CA2012

    await using WebSocketRealtimeSocket adapter = new(ws);
    await adapter.EmitAsync("pair:ready", new { peerRole = "tablet" });

    captured.Count.ShouldBe(1);
    string text = Encoding.UTF8.GetString(captured[0]);
    using var doc = JsonDocument.Parse(text);
    doc.RootElement.GetProperty("event").GetString().ShouldBe("pair:ready");
    doc.RootElement.GetProperty("data").GetProperty("peerRole").GetString().ShouldBe("tablet");
  }

  [Fact]
  public async Task DisconnectAsync_SendsCloseFrame_AndIsIdempotent()
  {
    WebSocket ws = Substitute.For<WebSocket>();
    ws.State.Returns(WebSocketState.Open);

    await using WebSocketRealtimeSocket adapter = new(ws);
    await adapter.DisconnectAsync("test");
    await adapter.DisconnectAsync("test"); // second call must not throw

    await ws.Received(1).CloseAsync(WebSocketCloseStatus.NormalClosure, Arg.Any<string>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task EmitAsync_WhenSocketClosed_DoesNotThrow()
  {
    WebSocket ws = Substitute.For<WebSocket>();
    ws.State.Returns(WebSocketState.Closed);

    await using WebSocketRealtimeSocket adapter = new(ws);
    await adapter.EmitAsync("session:push", new { });

    await ws.DidNotReceive().SendAsync(
      Arg.Any<ReadOnlyMemory<byte>>(),
      Arg.Any<WebSocketMessageType>(),
      Arg.Any<bool>(),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task EmitAsync_SendAsyncThrowsWebSocketException_DoesNotPropagate()
  {
    WebSocket ws = Substitute.For<WebSocket>();
    ws.State.Returns(WebSocketState.Open);
#pragma warning disable CA2012 // NSubstitute When/Do setup does not await the ValueTask
    ws.When(x => x.SendAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<WebSocketMessageType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()))
      .Do(_ => throw new WebSocketException("simulated concurrent close"));
#pragma warning restore CA2012

    await using var adapter = new WebSocketRealtimeSocket(ws);

    await adapter.EmitAsync("pair:ready", new { peerRole = "tablet" });
  }
}
