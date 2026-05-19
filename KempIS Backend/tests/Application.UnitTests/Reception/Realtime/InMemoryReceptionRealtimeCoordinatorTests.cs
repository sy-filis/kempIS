using Application.Abstractions.Reception;
using Application.Reception.PairCodes.Commands.CreatePairCode;
using Application.Reception.Realtime;
using Infrastructure.Reception;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.UnitTests.Reception.Realtime;

public sealed class InMemoryReceptionRealtimeCoordinatorTests
{
  private static InMemoryReceptionRealtimeCoordinator Build(
    out FakeDateTimeProvider clock,
    int pairCodeTtlSeconds = 120)
  {
    clock = new FakeDateTimeProvider(new DateTime(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc));
    ReceptionOptions options = new() { PairCodeTtlSeconds = pairCodeTtlSeconds };
    return new InMemoryReceptionRealtimeCoordinator(
      clock,
      Options.Create(options),
      NullLogger<InMemoryReceptionRealtimeCoordinator>.Instance);
  }

  [Fact]
  public void IssuePairCode_ReturnsBase64UrlString_OfExpectedLength()
  {
    using InMemoryReceptionRealtimeCoordinator sut = Build(out _);

    CreatePairCodeResponse response = sut.IssuePairCode();

    response.PairCode.ShouldNotBeNullOrEmpty();
    response.PairCode.Length.ShouldBeInRange(42, 44);
    response.PairCode.ShouldMatch("^[A-Za-z0-9_-]+$");
  }

  [Fact]
  public void IssuePairCode_SetsExpiryAtUtcPlusTtl()
  {
    using InMemoryReceptionRealtimeCoordinator sut = Build(out FakeDateTimeProvider clock, pairCodeTtlSeconds: 120);

    CreatePairCodeResponse response = sut.IssuePairCode();

    response.ExpiresAtUtc.ShouldBe(clock.UtcNow.AddSeconds(120));
  }

  [Fact]
  public void IssuePairCode_GeneratesDistinctCodesAcrossCalls()
  {
    using InMemoryReceptionRealtimeCoordinator sut = Build(out _);

    string a = sut.IssuePairCode().PairCode;
    string b = sut.IssuePairCode().PairCode;

    a.ShouldNotBe(b);
  }

  [Fact]
  public async Task TryJoinRoom_FirstPeer_StoresAndReturnsRoomNotReady()
  {
    using InMemoryReceptionRealtimeCoordinator sut = Build(out _);
    string code = sut.IssuePairCode().PairCode;
    FakeRealtimeSocket desktop = new();

    Result<RoomJoinOutcome> result = await sut.TryJoinRoomAsync(code, PeerRole.Desktop, desktop);

    result.IsSuccess.ShouldBeTrue();
    result.Value.RoomReady.ShouldBeFalse();
    result.Value.OtherPeer.ShouldBeNull();
    desktop.Emitted.ShouldBeEmpty();
  }

  [Fact]
  public async Task TryJoinRoom_BothPeersJoin_EmitsPairReadyToBoth_AndRemovesCodeFromAllowlist()
  {
    using InMemoryReceptionRealtimeCoordinator sut = Build(out _);
    string code = sut.IssuePairCode().PairCode;
    FakeRealtimeSocket desktop = new();
    FakeRealtimeSocket tablet = new();

    await sut.TryJoinRoomAsync(code, PeerRole.Desktop, desktop);
    Result<RoomJoinOutcome> result = await sut.TryJoinRoomAsync(code, PeerRole.Tablet, tablet);

    result.IsSuccess.ShouldBeTrue();
    result.Value.RoomReady.ShouldBeTrue();
    result.Value.OtherPeer.ShouldBe(PeerRole.Desktop);

    desktop.Emitted.ShouldContain(e => e.Event == "pair:ready");
    tablet.Emitted.ShouldContain(e => e.Event == "pair:ready");

    FakeRealtimeSocket intruder = new();
    Result<RoomJoinOutcome> reject = await sut.TryJoinRoomAsync(code, PeerRole.Tablet, intruder);
    reject.IsFailure.ShouldBeTrue();
    reject.Error.ShouldBe(RealtimeErrors.InvalidPairCode);
  }

  [Fact]
  public async Task TryJoinRoom_WithUnknownCode_ReturnsInvalidPairCode()
  {
    using InMemoryReceptionRealtimeCoordinator sut = Build(out _);
    FakeRealtimeSocket socket = new();

    Result<RoomJoinOutcome> result = await sut.TryJoinRoomAsync("bogus", PeerRole.Tablet, socket);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(RealtimeErrors.InvalidPairCode);
  }

  [Fact]
  public async Task TryJoinRoom_WithExpiredCode_ReturnsInvalidPairCode()
  {
    using InMemoryReceptionRealtimeCoordinator sut = Build(out FakeDateTimeProvider clock, pairCodeTtlSeconds: 120);
    string code = sut.IssuePairCode().PairCode;
    FakeRealtimeSocket socket = new();

    clock.Advance(TimeSpan.FromSeconds(121));

    Result<RoomJoinOutcome> result = await sut.TryJoinRoomAsync(code, PeerRole.Tablet, socket);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(RealtimeErrors.InvalidPairCode);
  }

  [Fact]
  public async Task TryJoinRoom_SameRoleTwice_ReturnsRoleTaken()
  {
    using InMemoryReceptionRealtimeCoordinator sut = Build(out _);
    string code = sut.IssuePairCode().PairCode;
    FakeRealtimeSocket firstTablet = new();
    FakeRealtimeSocket secondTablet = new();

    await sut.TryJoinRoomAsync(code, PeerRole.Tablet, firstTablet);
    Result<RoomJoinOutcome> result = await sut.TryJoinRoomAsync(code, PeerRole.Tablet, secondTablet);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(RealtimeErrors.RoleTaken);
  }

  [Fact]
  public async Task TryJoinRoom_NewPairBecomesReady_DisplacesPreviousReadyPair()
  {
    using InMemoryReceptionRealtimeCoordinator sut = Build(out _);

    string firstCode = sut.IssuePairCode().PairCode;
    FakeRealtimeSocket oldDesktop = new();
    FakeRealtimeSocket oldTablet = new();
    await sut.TryJoinRoomAsync(firstCode, PeerRole.Desktop, oldDesktop);
    await sut.TryJoinRoomAsync(firstCode, PeerRole.Tablet, oldTablet);

    string secondCode = sut.IssuePairCode().PairCode;
    FakeRealtimeSocket newDesktop = new();
    FakeRealtimeSocket newTablet = new();
    await sut.TryJoinRoomAsync(secondCode, PeerRole.Desktop, newDesktop);
    Result<RoomJoinOutcome> result = await sut.TryJoinRoomAsync(secondCode, PeerRole.Tablet, newTablet);

    result.IsSuccess.ShouldBeTrue();
    result.Value.RoomReady.ShouldBeTrue();

    oldDesktop.Emitted.ShouldContain(e => e.Event == "pair:displaced");
    oldTablet.Emitted.ShouldContain(e => e.Event == "pair:displaced");
    oldDesktop.IsConnected.ShouldBeFalse();
    oldTablet.IsConnected.ShouldBeFalse();

    newDesktop.Emitted.ShouldContain(e => e.Event == "pair:ready");
    newTablet.Emitted.ShouldContain(e => e.Event == "pair:ready");
    newDesktop.IsConnected.ShouldBeTrue();
    newTablet.IsConnected.ShouldBeTrue();
  }

  [Fact]
  public async Task TryJoinRoom_HalfJoinedNewRoom_DoesNotDisplaceReadyPair()
  {
    using InMemoryReceptionRealtimeCoordinator sut = Build(out _);

    string firstCode = sut.IssuePairCode().PairCode;
    FakeRealtimeSocket oldDesktop = new();
    FakeRealtimeSocket oldTablet = new();
    await sut.TryJoinRoomAsync(firstCode, PeerRole.Desktop, oldDesktop);
    await sut.TryJoinRoomAsync(firstCode, PeerRole.Tablet, oldTablet);

    string secondCode = sut.IssuePairCode().PairCode;
    FakeRealtimeSocket lonely = new();
    Result<RoomJoinOutcome> result = await sut.TryJoinRoomAsync(secondCode, PeerRole.Desktop, lonely);

    result.IsSuccess.ShouldBeTrue();
    result.Value.RoomReady.ShouldBeFalse();
    oldDesktop.IsConnected.ShouldBeTrue();
    oldDesktop.Emitted.ShouldNotContain(e => e.Event == "pair:displaced");
  }

  [Fact]
  public async Task RemoveSocket_OnReadyRoomMember_TearsDownRoom_EmitsPeerLeftToOther()
  {
    using InMemoryReceptionRealtimeCoordinator sut = Build(out _);
    string code = sut.IssuePairCode().PairCode;
    FakeRealtimeSocket desktop = new();
    FakeRealtimeSocket tablet = new();
    await sut.TryJoinRoomAsync(code, PeerRole.Desktop, desktop);
    await sut.TryJoinRoomAsync(code, PeerRole.Tablet, tablet);

    await sut.RemoveSocketAsync(tablet);

    desktop.Emitted.ShouldContain(e => e.Event == "pair:peer_left");
  }

  [Fact]
  public async Task RemoveSocket_OnPendingRoomMember_DropsRoom()
  {
    using InMemoryReceptionRealtimeCoordinator sut = Build(out _);
    string code = sut.IssuePairCode().PairCode;
    FakeRealtimeSocket desktop = new();
    await sut.TryJoinRoomAsync(code, PeerRole.Desktop, desktop);

    await sut.RemoveSocketAsync(desktop);

    // Pending room never reached ready, so the code stays in the allowlist; the new tablet
    // socket joins as the first peer of a fresh pending room.
    FakeRealtimeSocket tablet = new();
    Result<RoomJoinOutcome> result = await sut.TryJoinRoomAsync(code, PeerRole.Tablet, tablet);
    result.IsSuccess.ShouldBeTrue();
    result.Value.RoomReady.ShouldBeFalse();
  }

  [Fact]
  public async Task RemoveSocket_OnUnknownSocket_Noops()
  {
    using InMemoryReceptionRealtimeCoordinator sut = Build(out _);
    FakeRealtimeSocket stranger = new();

    await sut.RemoveSocketAsync(stranger); // must not throw.
  }

  [Fact]
  public async Task GetOtherSocket_AfterReady_ReturnsThePeer()
  {
    using InMemoryReceptionRealtimeCoordinator sut = Build(out _);
    string code = sut.IssuePairCode().PairCode;
    FakeRealtimeSocket desktop = new();
    FakeRealtimeSocket tablet = new();
    await sut.TryJoinRoomAsync(code, PeerRole.Desktop, desktop);
    await sut.TryJoinRoomAsync(code, PeerRole.Tablet, tablet);

    sut.GetOtherSocket(desktop).ShouldBe(tablet);
    sut.GetOtherSocket(tablet).ShouldBe(desktop);
  }

  [Fact]
  public async Task GetOtherSocket_BeforeReady_ReturnsNull()
  {
    using InMemoryReceptionRealtimeCoordinator sut = Build(out _);
    string code = sut.IssuePairCode().PairCode;
    FakeRealtimeSocket desktop = new();
    await sut.TryJoinRoomAsync(code, PeerRole.Desktop, desktop);

    sut.GetOtherSocket(desktop).ShouldBeNull();
  }

  [Fact]
  public async Task Sweep_RemovesExpiredAllowlistEntries()
  {
    using InMemoryReceptionRealtimeCoordinator sut = Build(out FakeDateTimeProvider clock, pairCodeTtlSeconds: 60);
    string code = sut.IssuePairCode().PairCode;

    clock.Advance(TimeSpan.FromSeconds(61));
    sut.SweepExpiredAllowlistEntries();

    FakeRealtimeSocket socket = new();
    Result<RoomJoinOutcome> result = await sut.TryJoinRoomAsync(code, PeerRole.Tablet, socket);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(RealtimeErrors.InvalidPairCode);
  }
}
