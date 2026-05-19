using System.Collections.Generic;
using System.Net.Http.Json;
using Application.Abstractions.Authentication;
using Application.Reception.PairCodes.Commands.CreatePairCode;
using Application.Reception.Realtime;
using Microsoft.Extensions.Configuration;
using TestUtilities.Realtime;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reception;

public sealed class ReceptionRealtimeWebSocketPairingTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public ReceptionRealtimeWebSocketPairingTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync() { _factory.CreateClient().Dispose(); return Task.CompletedTask; }
  public Task DisposeAsync() => Task.CompletedTask;

  private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

  private async Task<CreatePairCodeResponse> IssuePairCodeAsync()
  {
    HttpClient http = _factory.CreateClient();
    http.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, Roles.Receptionist);
    HttpResponseMessage r = await http.PostAsync(new Uri("reception/pair-codes", UriKind.Relative), content: null);
    r.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
    return (await r.Content.ReadFromJsonAsync<CreatePairCodeResponse>())!;
  }

  private Task<RealtimeWebSocketTestClient> ConnectAsync()
  {
    Microsoft.AspNetCore.TestHost.WebSocketClient wsClient = _factory.Server.CreateWebSocketClient();
    Uri uri = new(_factory.Server.BaseAddress, "api/reception/realtime");
    return RealtimeWebSocketTestClient.ConnectAsync((u, ct) => wsClient.ConnectAsync(u, ct), uri);
  }

  [Fact]
  public async Task BothPeersJoin_ReceivePairReady_AndDesktopRelaysSessionPushToTablet()
  {
    CreatePairCodeResponse code = await IssuePairCodeAsync();

    using RealtimeWebSocketTestClient desktop = await ConnectAsync();
    using RealtimeWebSocketTestClient tablet = await ConnectAsync();

    await desktop.EmitAsync(ReceptionEventNames.PairJoin, new { pairCode = code.PairCode, role = "desktop" });
    await tablet.EmitAsync(ReceptionEventNames.PairJoin, new { pairCode = code.PairCode, role = "tablet" });

    RealtimeEcho desktopReady = await desktop.WaitForAsync(ReceptionEventNames.PairReady, EventTimeout);
    RealtimeEcho tabletReady = await tablet.WaitForAsync(ReceptionEventNames.PairReady, EventTimeout);
    desktopReady.Data.GetProperty("peerRole").GetString().ShouldBe("tablet");
    tabletReady.Data.GetProperty("peerRole").GetString().ShouldBe("desktop");

    await desktop.EmitAsync(ReceptionEventNames.SessionPush, new { bill = new { number = "B-1" }, guests = Array.Empty<object>() });
    RealtimeEcho push = await tablet.WaitForAsync(ReceptionEventNames.SessionPush, EventTimeout);
    push.Data.GetProperty("bill").GetProperty("number").GetString().ShouldBe("B-1");
  }

  [Fact]
  public async Task TabletDisconnects_DesktopReceivesPairPeerLeft()
  {
    CreatePairCodeResponse code = await IssuePairCodeAsync();

    using RealtimeWebSocketTestClient desktop = await ConnectAsync();
    RealtimeWebSocketTestClient tablet = await ConnectAsync();

    await desktop.EmitAsync(ReceptionEventNames.PairJoin, new { pairCode = code.PairCode, role = "desktop" });
    await tablet.EmitAsync(ReceptionEventNames.PairJoin, new { pairCode = code.PairCode, role = "tablet" });
    await desktop.WaitForAsync(ReceptionEventNames.PairReady, EventTimeout);

    tablet.Dispose();

    RealtimeEcho left = await desktop.WaitForAsync(ReceptionEventNames.PairPeerLeft, EventTimeout);
    left.Data.GetProperty("peerRole").GetString().ShouldBe("tablet");
  }

  [Fact]
  public async Task InvalidPairCode_EmitsErrorAndCloses()
  {
    using RealtimeWebSocketTestClient tablet = await ConnectAsync();

    Task<RealtimeEcho> errTask = tablet.WaitForAsync(ReceptionEventNames.Error, EventTimeout);
    await tablet.EmitAsync(ReceptionEventNames.PairJoin, new { pairCode = "bogus-bogus-bogus", role = "tablet" });

    RealtimeEcho err = await errTask;
    err.Data.GetProperty("code").GetString().ShouldBe("invalid_pair_code");

    await tablet.WaitForDisconnectAsync(EventTimeout);
    tablet.CloseStatus.ShouldBe(System.Net.WebSockets.WebSocketCloseStatus.PolicyViolation);
  }

  [Fact]
  public async Task MalformedFrame_EmitsBadRequestAndCloses()
  {
    using RealtimeWebSocketTestClient tablet = await ConnectAsync();

    // Use a fresh raw WebSocket so we can send a non-JSON payload directly,
    // bypassing the test-client's envelope formatting.
    Microsoft.AspNetCore.TestHost.WebSocketClient wsClient = _factory.Server.CreateWebSocketClient();
    var uri = new Uri(_factory.Server.BaseAddress, "api/reception/realtime");
    System.Net.WebSockets.WebSocket raw = await wsClient.ConnectAsync(uri, CancellationToken.None);
    try
    {
      byte[] junk = System.Text.Encoding.UTF8.GetBytes("not-json");
      await raw.SendAsync(junk.AsMemory(), System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);

      byte[] buf = new byte[1024];
      System.Net.WebSockets.WebSocketReceiveResult result = await raw.ReceiveAsync(buf, CancellationToken.None);
      string text = System.Text.Encoding.UTF8.GetString(buf, 0, result.Count);
      text.ShouldContain("\"code\":\"bad_request\"");

      while (raw.State == System.Net.WebSockets.WebSocketState.Open)
      {
        System.Net.WebSockets.WebSocketReceiveResult r = await raw.ReceiveAsync(buf, CancellationToken.None);
        if (r.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
        {
          break;
        }
      }

      raw.CloseStatus.ShouldBe(System.Net.WebSockets.WebSocketCloseStatus.PolicyViolation);
    }
    finally
    {
      raw.Dispose();
    }

    // The unrelated tablet WS is unaffected.
    tablet.CloseStatus.ShouldBeNull();
  }

  [Fact]
  public async Task OversizeSessionPush_EmitsPayloadTooLarge_StaysOpen()
  {
    CreatePairCodeResponse code = await IssuePairCodeAsync();

    using RealtimeWebSocketTestClient desktop = await ConnectAsync();
    using RealtimeWebSocketTestClient tablet = await ConnectAsync();

    await desktop.EmitAsync(ReceptionEventNames.PairJoin, new { pairCode = code.PairCode, role = "desktop" });
    await tablet.EmitAsync(ReceptionEventNames.PairJoin, new { pairCode = code.PairCode, role = "tablet" });
    await desktop.WaitForAsync(ReceptionEventNames.PairReady, EventTimeout);

    Task<RealtimeEcho> errTask = desktop.WaitForAsync(ReceptionEventNames.Error, EventTimeout);

    // Pad payload past the default SessionPushMaxBytes (65 536).
    string padding = new string('a', 70_000);
    await desktop.EmitAsync(ReceptionEventNames.SessionPush, new { padding });

    RealtimeEcho err = await errTask;
    err.Data.GetProperty("code").GetString().ShouldBe("payload_too_large");

    // Connection stays alive; a follow-up event still relays.
    await desktop.EmitAsync(ReceptionEventNames.SessionClear, new { });
    await tablet.WaitForAsync(ReceptionEventNames.SessionClear, EventTimeout);
  }
}

public sealed class ShortGraceApiFactory : ApiFactory
{
  protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
  {
    base.ConfigureWebHost(builder);
    builder.ConfigureAppConfiguration((_, config) =>
    {
      config.AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["Reception:TabletJoinGraceSeconds"] = "1",
      });
    });
  }
}

public sealed class ReceptionRealtimeGraceTimeoutTests : IClassFixture<ShortGraceApiFactory>, IAsyncLifetime
{
  private readonly ShortGraceApiFactory _factory;

  public ReceptionRealtimeGraceTimeoutTests(ShortGraceApiFactory factory) => _factory = factory;

  public Task InitializeAsync() { _factory.CreateClient().Dispose(); return Task.CompletedTask; }
  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task NoPairJoin_ClosesAfterGracePeriod()
  {
    Microsoft.AspNetCore.TestHost.WebSocketClient wsClient = _factory.Server.CreateWebSocketClient();
    var uri = new Uri(_factory.Server.BaseAddress, "api/reception/realtime");
    using RealtimeWebSocketTestClient tablet = await RealtimeWebSocketTestClient.ConnectAsync(
      (u, ct) => wsClient.ConnectAsync(u, ct), uri);

    await tablet.WaitForDisconnectAsync(TimeSpan.FromSeconds(5));
    tablet.CloseStatus.ShouldBe(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure);
  }
}
