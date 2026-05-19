using Application.Abstractions.Addresses;
using Application.Addresses.Queries.SuggestAddresses;
using Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Application.UnitTests.Addresses.Queries.SuggestAddresses;

public sealed class SuggestAddressesQueryHandlerTests
{
  private static readonly IReadOnlyList<AddressSuggestion> SamplePayload =
  [
    new AddressSuggestion("CZ", "Jedovnice", "67906", "Havlíčkovo náměstí", "71"),
  ];

  private static readonly IReadOnlyList<AddressSuggestion> Empty =
    Array.Empty<AddressSuggestion>();

  private readonly IAddressSuggester _ruian = Substitute.For<IAddressSuggester>();
  private readonly IAddressSuggester _mapy = Substitute.For<IAddressSuggester>();
  private readonly IAddressSuggestionCache _cache = Substitute.For<IAddressSuggestionCache>();
  private readonly SuggestAddressesQueryHandler _handler;

  public SuggestAddressesQueryHandlerTests()
  {
    IServiceCollection services = new ServiceCollection();
    services.AddKeyedScoped(AddressProvider.Ruian, (_, _) => _ruian);
    services.AddKeyedScoped(AddressProvider.Mapy, (_, _) => _mapy);
    ServiceProvider sp = services.BuildServiceProvider();

    _handler = new SuggestAddressesQueryHandler(
      sp.GetRequiredKeyedService<IAddressSuggester>(AddressProvider.Ruian),
      sp.GetRequiredKeyedService<IAddressSuggester>(AddressProvider.Mapy),
      _cache);
  }

  [Fact]
  public async Task Handle_WithQueryOfExactly8Chars_ReturnsValidationError()
  {
    Result<IReadOnlyList<AddressSuggestion>> result = await _handler.Handle(
      new SuggestAddressesQuery("12345678", Foreign: false, Limit: 5),
      CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Type.ShouldBe(ErrorType.Validation);
    await _ruian.DidNotReceiveWithAnyArgs().SuggestAsync(default!, default, default);
    await _mapy.DidNotReceiveWithAnyArgs().SuggestAsync(default!, default, default);
  }

  [Fact]
  public async Task Handle_WithWhitespaceOnlyQuery_ReturnsValidationError()
  {
    Result<IReadOnlyList<AddressSuggestion>> result = await _handler.Handle(
      new SuggestAddressesQuery("          ", Foreign: false, Limit: 5),
      CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Type.ShouldBe(ErrorType.Validation);
  }

  [Fact]
  public async Task Handle_WithLimitZero_ReturnsValidationError()
  {
    Result<IReadOnlyList<AddressSuggestion>> result = await _handler.Handle(
      new SuggestAddressesQuery("Havlíčkovo náměstí 71", Foreign: false, Limit: 0),
      CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Type.ShouldBe(ErrorType.Validation);
  }

  [Fact]
  public async Task Handle_WithNegativeLimit_ReturnsValidationError()
  {
    Result<IReadOnlyList<AddressSuggestion>> result = await _handler.Handle(
      new SuggestAddressesQuery("Havlíčkovo náměstí 71", Foreign: false, Limit: -1),
      CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Type.ShouldBe(ErrorType.Validation);
  }

  [Fact]
  public async Task Handle_CacheHit_ReturnsCachedAndDoesNotCallProviders()
  {
    const string query = "Havlíčkovo náměstí 71";
    _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
      .Returns(SamplePayload);

    Result<IReadOnlyList<AddressSuggestion>> result = await _handler.Handle(
      new SuggestAddressesQuery(query, Foreign: false, Limit: 5),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBe(SamplePayload);
    await _ruian.DidNotReceiveWithAnyArgs().SuggestAsync(default!, default, default);
    await _mapy.DidNotReceiveWithAnyArgs().SuggestAsync(default!, default, default);
  }

  [Fact]
  public async Task Handle_ForeignFalse_RuianNonEmpty_CachesAndReturnsRuian()
  {
    _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
      .Returns((IReadOnlyList<AddressSuggestion>?)null);
    _ruian.SuggestAsync("havlíčkovo náměstí 71", 5, Arg.Any<CancellationToken>())
      .Returns(Result.Success<IReadOnlyList<AddressSuggestion>>(SamplePayload));

    Result<IReadOnlyList<AddressSuggestion>> result = await _handler.Handle(
      new SuggestAddressesQuery("Havlíčkovo náměstí 71", Foreign: false, Limit: 5),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBe(SamplePayload);

    await _cache.Received(1).SetAsync(
      Arg.Any<string>(),
      SamplePayload,
      TimeSpan.FromDays(180),
      Arg.Any<CancellationToken>());
    await _mapy.DidNotReceiveWithAnyArgs().SuggestAsync(default!, default, default);
  }

  [Fact]
  public async Task Handle_ForeignFalse_RuianEmpty_FallsBackToMapy()
  {
    _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
      .Returns((IReadOnlyList<AddressSuggestion>?)null);
    _ruian.SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success<IReadOnlyList<AddressSuggestion>>(Empty));
    _mapy.SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success<IReadOnlyList<AddressSuggestion>>(SamplePayload));

    Result<IReadOnlyList<AddressSuggestion>> result = await _handler.Handle(
      new SuggestAddressesQuery("Havlíčkovo náměstí 71", Foreign: false, Limit: 5),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBe(SamplePayload);
    await _mapy.Received(1).SuggestAsync(Arg.Any<string>(), 5, Arg.Any<CancellationToken>());
    await _cache.Received(1).SetAsync(
      Arg.Any<string>(),
      SamplePayload,
      TimeSpan.FromDays(180),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_ForeignFalse_RuianErrors_FallsBackToMapy()
  {
    _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
      .Returns((IReadOnlyList<AddressSuggestion>?)null);
    _ruian.SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Failure<IReadOnlyList<AddressSuggestion>>(AddressErrors.ProviderUnavailable));
    _mapy.SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success<IReadOnlyList<AddressSuggestion>>(SamplePayload));

    Result<IReadOnlyList<AddressSuggestion>> result = await _handler.Handle(
      new SuggestAddressesQuery("Havlíčkovo náměstí 71", Foreign: false, Limit: 5),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBe(SamplePayload);
  }

  [Fact]
  public async Task Handle_ForeignFalse_BothEmpty_CachesEmptyForOneDay()
  {
    _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
      .Returns((IReadOnlyList<AddressSuggestion>?)null);
    _ruian.SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success<IReadOnlyList<AddressSuggestion>>(Empty));
    _mapy.SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success<IReadOnlyList<AddressSuggestion>>(Empty));

    Result<IReadOnlyList<AddressSuggestion>> result = await _handler.Handle(
      new SuggestAddressesQuery("Havlíčkovo náměstí 71", Foreign: false, Limit: 5),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBeEmpty();
    await _cache.Received(1).SetAsync(
      Arg.Any<string>(),
      Arg.Is<IReadOnlyList<AddressSuggestion>>(v => v.Count == 0),
      TimeSpan.FromDays(1),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_ForeignFalse_RuianErrors_MapyErrors_ReturnsProviderUnavailable_DoesNotCache()
  {
    _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
      .Returns((IReadOnlyList<AddressSuggestion>?)null);
    _ruian.SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Failure<IReadOnlyList<AddressSuggestion>>(AddressErrors.ProviderUnavailable));
    _mapy.SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Failure<IReadOnlyList<AddressSuggestion>>(AddressErrors.ProviderUnavailable));

    Result<IReadOnlyList<AddressSuggestion>> result = await _handler.Handle(
      new SuggestAddressesQuery("Havlíčkovo náměstí 71", Foreign: false, Limit: 5),
      CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(AddressErrors.ProviderUnavailable);
    await _cache.DidNotReceiveWithAnyArgs().SetAsync(default!, default!, default, default);
  }

  [Fact]
  public async Task Handle_ForeignTrue_CallsMapyOnly_NoFallback()
  {
    _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
      .Returns((IReadOnlyList<AddressSuggestion>?)null);
    _mapy.SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success<IReadOnlyList<AddressSuggestion>>(SamplePayload));

    Result<IReadOnlyList<AddressSuggestion>> result = await _handler.Handle(
      new SuggestAddressesQuery("Some German Street 12", Foreign: true, Limit: 5),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    await _ruian.DidNotReceiveWithAnyArgs().SuggestAsync(default!, default, default);
  }

  [Fact]
  public async Task Handle_ForeignTrue_MapyErrors_ReturnsProviderUnavailable()
  {
    _cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
      .Returns((IReadOnlyList<AddressSuggestion>?)null);
    _mapy.SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Failure<IReadOnlyList<AddressSuggestion>>(AddressErrors.ProviderUnavailable));

    Result<IReadOnlyList<AddressSuggestion>> result = await _handler.Handle(
      new SuggestAddressesQuery("Some German Street 12", Foreign: true, Limit: 5),
      CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(AddressErrors.ProviderUnavailable);
    await _ruian.DidNotReceiveWithAnyArgs().SuggestAsync(default!, default, default);
  }

  [Fact]
  public async Task Handle_NormalizesQuery_DifferentCasingHitsSameCacheKey()
  {
    string? capturedKeyA = null;
    string? capturedKeyB = null;

    _cache.GetAsync(Arg.Do<string>(k => capturedKeyA ??= k), Arg.Any<CancellationToken>())
      .Returns((IReadOnlyList<AddressSuggestion>?)null);
    _ruian.SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success<IReadOnlyList<AddressSuggestion>>(SamplePayload));

    await _handler.Handle(new SuggestAddressesQuery("HAVLÍČKOVO  NÁMĚSTÍ 71", false, 5), default);

    _cache.ClearReceivedCalls();
    _cache.GetAsync(Arg.Do<string>(k => capturedKeyB ??= k), Arg.Any<CancellationToken>())
      .Returns((IReadOnlyList<AddressSuggestion>?)null);

    await _handler.Handle(new SuggestAddressesQuery("  havlíčkovo náměstí 71  ", false, 5), default);

    capturedKeyA.ShouldNotBeNull();
    capturedKeyA.ShouldBe(capturedKeyB);
  }
}
