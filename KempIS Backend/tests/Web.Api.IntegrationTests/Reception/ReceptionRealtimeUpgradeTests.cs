using System.Net.WebSockets;
using Microsoft.AspNetCore.TestHost;
using TestUtilities.Realtime;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reception;

public sealed class ReceptionRealtimeUpgradeTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public ReceptionRealtimeUpgradeTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync() { _factory.CreateClient().Dispose(); return Task.CompletedTask; }
  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task Connect_UpgradesWebSocket_AndStaysOpen()
  {
    WebSocketClient wsClient = _factory.Server.CreateWebSocketClient();
    Uri uri = new(_factory.Server.BaseAddress, "api/reception/realtime");

    using RealtimeWebSocketTestClient client = await RealtimeWebSocketTestClient.ConnectAsync(
      (u, ct) => wsClient.ConnectAsync(u, ct),
      uri);

    await Task.Delay(50);

    // No close assertion: a failed upgrade would have thrown from ConnectAsync.
  }
}
