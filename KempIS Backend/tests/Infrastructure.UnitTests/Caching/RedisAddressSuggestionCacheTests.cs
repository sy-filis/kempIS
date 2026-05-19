using System.Text.Json;
using Application.Addresses.Queries.SuggestAddresses;
using Infrastructure.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infrastructure.UnitTests.Caching;

public sealed class RedisAddressSuggestionCacheTests
{
  private readonly IDistributedCache _underlying = Substitute.For<IDistributedCache>();
  private readonly RedisAddressSuggestionCache _sut;

  public RedisAddressSuggestionCacheTests()
  {
    _sut = new RedisAddressSuggestionCache(_underlying, NullLogger<RedisAddressSuggestionCache>.Instance);
  }

  private static readonly IReadOnlyList<AddressSuggestion> SamplePayload =
  [
    new AddressSuggestion("CZ", "Jedovnice", "67906", "Havlíčkovo náměstí", "71"),
  ];

  [Fact]
  public async Task GetAsync_WithStoredBytes_DeserializesPayload()
  {
    byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(SamplePayload);
    _underlying.GetAsync("key", Arg.Any<CancellationToken>())
      .Returns(serialized);

    IReadOnlyList<AddressSuggestion>? result = await _sut.GetAsync("key", CancellationToken.None);

    result.ShouldNotBeNull();
    result.ShouldBe(SamplePayload);
  }

  [Fact]
  public async Task GetAsync_WithMissingKey_ReturnsNull()
  {
    _underlying.GetAsync("missing", Arg.Any<CancellationToken>())
      .Returns((byte[]?)null);

    IReadOnlyList<AddressSuggestion>? result = await _sut.GetAsync("missing", CancellationToken.None);

    result.ShouldBeNull();
  }

  [Fact]
  public async Task GetAsync_UnderlyingCacheThrows_ReturnsNullAndDoesNotPropagate()
  {
    _underlying.GetAsync("key", Arg.Any<CancellationToken>())
      .Returns<byte[]?>(_ => throw new InvalidOperationException("redis down"));

    IReadOnlyList<AddressSuggestion>? result = await _sut.GetAsync("key", CancellationToken.None);

    result.ShouldBeNull();
  }

  [Fact]
  public async Task GetAsync_OperationCanceledException_Propagates()
  {
    using var cts = new CancellationTokenSource();
    await cts.CancelAsync();
    _underlying.GetAsync("key", Arg.Any<CancellationToken>())
      .Returns<byte[]?>(_ => throw new OperationCanceledException(cts.Token));

    await Should.ThrowAsync<OperationCanceledException>(
      () => _sut.GetAsync("key", cts.Token));
  }

  [Fact]
  public async Task SetAsync_SerializesPayloadAndWritesWithTtl()
  {
    DistributedCacheEntryOptions? capturedOptions = null;
    byte[]? capturedBytes = null;

    await _underlying.SetAsync(
      Arg.Any<string>(),
      Arg.Do<byte[]>(b => capturedBytes = b),
      Arg.Do<DistributedCacheEntryOptions>(o => capturedOptions = o),
      Arg.Any<CancellationToken>());

    var ttl = TimeSpan.FromHours(6);
    await _sut.SetAsync("key", SamplePayload, ttl, CancellationToken.None);

    capturedBytes.ShouldNotBeNull();
    IReadOnlyList<AddressSuggestion>? roundTripped =
      JsonSerializer.Deserialize<IReadOnlyList<AddressSuggestion>>(capturedBytes);
    roundTripped.ShouldBe(SamplePayload);

    capturedOptions.ShouldNotBeNull();
    capturedOptions.AbsoluteExpirationRelativeToNow.ShouldBe(ttl);
  }

  [Fact]
  public async Task SetAsync_UnderlyingCacheThrows_DoesNotPropagate()
  {
    _underlying.SetAsync(
      Arg.Any<string>(),
      Arg.Any<byte[]>(),
      Arg.Any<DistributedCacheEntryOptions>(),
      Arg.Any<CancellationToken>())
      .Returns<Task>(_ => Task.FromException(new InvalidOperationException("redis down")));

    await Should.NotThrowAsync(
      () => _sut.SetAsync("key", SamplePayload, TimeSpan.FromMinutes(1), CancellationToken.None));
  }

  [Fact]
  public async Task SetAsync_OperationCanceledException_Propagates()
  {
    using var cts = new CancellationTokenSource();
    await cts.CancelAsync();
    _underlying.SetAsync(
      Arg.Any<string>(),
      Arg.Any<byte[]>(),
      Arg.Any<DistributedCacheEntryOptions>(),
      Arg.Any<CancellationToken>())
      .Returns<Task>(_ => Task.FromException(new OperationCanceledException(cts.Token)));

    await Should.ThrowAsync<OperationCanceledException>(
      () => _sut.SetAsync("key", SamplePayload, TimeSpan.FromMinutes(1), cts.Token));
  }
}
