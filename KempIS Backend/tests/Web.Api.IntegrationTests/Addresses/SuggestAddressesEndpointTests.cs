using Application.Abstractions.Addresses;
using Application.Abstractions.Authentication;
using Application.Addresses.Queries.SuggestAddresses;
using Domain.Common;
using SharedKernel;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Addresses;

public sealed class SuggestAddressesEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public SuggestAddressesEndpointTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync()
  {
    _factory.AddressCache.Clear();
    _factory.RuianSuggester.ClearReceivedCalls();
    _factory.MapySuggester.ClearReceivedCalls();
    _factory.RuianSuggester.SuggestAsync(default!, default, default)
      .ReturnsForAnyArgs(Result.Success<IReadOnlyList<AddressSuggestion>>(Array.Empty<AddressSuggestion>()));
    _factory.MapySuggester.SuggestAsync(default!, default, default)
      .ReturnsForAnyArgs(Result.Success<IReadOnlyList<AddressSuggestion>>(Array.Empty<AddressSuggestion>()));
    return Task.CompletedTask;
  }

  public Task DisposeAsync() => Task.CompletedTask;

  private HttpClient Client(params string[] roles)
  {
    HttpClient client = _factory.CreateClient();
    if (roles.Length > 0)
    {
      client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
    }
    return client;
  }

  private static readonly IReadOnlyList<AddressSuggestion> Sample =
  [
    new AddressSuggestion("CZ", "Jedovnice", "67906", "Havlíčkovo náměstí", "71"),
  ];

  [Fact]
  public async Task Get_AsAnonymous_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(
      new Uri("addresses/whisperer?query=Havlíčkovo+náměstí+71", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Get_AsAccountant_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.Accountant).GetAsync(
      new Uri("addresses/whisperer?query=Havlíčkovo+náměstí+71", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Get_WithQueryTooShort_Returns400()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("addresses/whisperer?query=12345678", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Get_WithLimitZero_Returns400()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("addresses/whisperer?query=Havlíčkovo+náměstí+71&limit=0", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Get_WithLimitTen_ClampedToFive()
  {
    _factory.RuianSuggester
      .SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success<IReadOnlyList<AddressSuggestion>>(Sample));

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("addresses/whisperer?query=Havlíčkovo+náměstí+71&limit=10", UriKind.Relative));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    await _factory.RuianSuggester.Received(1)
      .SuggestAsync(Arg.Any<string>(), 5, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Get_ForeignFalse_RuianReturnsResults_Returns200()
  {
    _factory.RuianSuggester
      .SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success<IReadOnlyList<AddressSuggestion>>(Sample));

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("addresses/whisperer?query=Havlíčkovo+náměstí+71", UriKind.Relative));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    List<AddressSuggestion>? body = await response.Content.ReadFromJsonAsync<List<AddressSuggestion>>();
    body.ShouldNotBeNull();
    body.ShouldHaveSingleItem();
    body[0].CountryCode.ShouldBe("CZ");
    body[0].City.ShouldBe("Jedovnice");
    body[0].ZipCode.ShouldBe("67906");
    body[0].Street.ShouldBe("Havlíčkovo náměstí");
    body[0].HouseNumber.ShouldBe("71");

    await _factory.MapySuggester.DidNotReceiveWithAnyArgs()
      .SuggestAsync(default!, default, default);
  }

  [Fact]
  public async Task Get_ForeignFalse_RuianEmpty_FallsBackToMapy()
  {
    _factory.RuianSuggester
      .SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success<IReadOnlyList<AddressSuggestion>>(Array.Empty<AddressSuggestion>()));
    _factory.MapySuggester
      .SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success<IReadOnlyList<AddressSuggestion>>(Sample));

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("addresses/whisperer?query=Havlíčkovo+náměstí+71", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    await _factory.MapySuggester.Received(1)
      .SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Get_ForeignFalse_RuianErrors_FallsBackToMapy()
  {
    _factory.RuianSuggester
      .SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Failure<IReadOnlyList<AddressSuggestion>>(AddressErrors.ProviderUnavailable));
    _factory.MapySuggester
      .SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success<IReadOnlyList<AddressSuggestion>>(Sample));

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("addresses/whisperer?query=Havlíčkovo+náměstí+71", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
  }

  [Fact]
  public async Task Get_ForeignFalse_BothEmpty_ReturnsEmptyArray()
  {
    _factory.RuianSuggester
      .SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success<IReadOnlyList<AddressSuggestion>>(Array.Empty<AddressSuggestion>()));
    _factory.MapySuggester
      .SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success<IReadOnlyList<AddressSuggestion>>(Array.Empty<AddressSuggestion>()));

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("addresses/whisperer?query=Havlíčkovo+náměstí+71", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    List<AddressSuggestion>? body = await response.Content.ReadFromJsonAsync<List<AddressSuggestion>>();
    body.ShouldNotBeNull();
    body.ShouldBeEmpty();
  }

  [Fact]
  public async Task Get_ForeignFalse_BothError_Returns500()
  {
    _factory.RuianSuggester
      .SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Failure<IReadOnlyList<AddressSuggestion>>(AddressErrors.ProviderUnavailable));
    _factory.MapySuggester
      .SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Failure<IReadOnlyList<AddressSuggestion>>(AddressErrors.ProviderUnavailable));

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("addresses/whisperer?query=Havlíčkovo+náměstí+71", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
  }

  [Fact]
  public async Task Get_ForeignTrue_UsesMapyOnly()
  {
    _factory.MapySuggester
      .SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success<IReadOnlyList<AddressSuggestion>>(Sample));

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("addresses/whisperer?query=Some+foreign+street+12&foreign=true", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    await _factory.RuianSuggester.DidNotReceiveWithAnyArgs()
      .SuggestAsync(default!, default, default);
    await _factory.MapySuggester.Received(1)
      .SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Get_CachedResponse_DoesNotCallProviders()
  {
    _factory.AddressCache.Seed("whisperer:v1:cz:5:havlíčkovo náměstí 71", Sample);

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("addresses/whisperer?query=Havlíčkovo+náměstí+71", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    await _factory.RuianSuggester.DidNotReceiveWithAnyArgs()
      .SuggestAsync(default!, default, default);
    await _factory.MapySuggester.DidNotReceiveWithAnyArgs()
      .SuggestAsync(default!, default, default);
  }
}
