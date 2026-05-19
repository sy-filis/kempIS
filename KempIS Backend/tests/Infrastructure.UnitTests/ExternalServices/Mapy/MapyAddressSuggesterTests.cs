using System.Net;
using Domain.Common;
using Infrastructure.ExternalServices.Mapy;
using Infrastructure.UnitTests.TestDoubles;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.UnitTests.ExternalServices.Mapy;

public sealed class MapyAddressSuggesterTests
{
  private const string BaseUrl = "https://mapy.test/v1/";

  private static HttpClient BuildClient(StubHttpMessageHandler handler) =>
    new(handler)
    {
      BaseAddress = new Uri(BaseUrl),
    };

  private static MapyAddressSuggester BuildSut(HttpClient client, string apiKey = "test-key") =>
    new(client, Options.Create(new MapyOptions { BaseUrl = BaseUrl, ApiKey = apiKey }));

  [Fact]
  public async Task SuggestAsync_FullRegionalStructure_MapsAllFields()
  {
    const string body = """
      {
        "items": [
          {
            "type": "regional.address",
            "location": "Jedovnice, Česko",
            "zip": "679 06",
            "regionalStructure": [
              { "name": "71", "type": "regional.address" },
              { "name": "Havlíčkovo náměstí", "type": "regional.street" },
              { "name": "Jedovnice", "type": "regional.municipality" },
              { "name": "Česko", "type": "regional.country", "isoCode": "CZ" }
            ]
          }
        ]
      }
      """;

    using var handler = new StubHttpMessageHandler(HttpStatusCode.OK, body);
    using HttpClient client = BuildClient(handler);
    MapyAddressSuggester sut = BuildSut(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("Havlíčkovo", 5, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldHaveSingleItem();
    result.Value[0].CountryCode.ShouldBe("CZ");
    result.Value[0].City.ShouldBe("Jedovnice");
    result.Value[0].ZipCode.ShouldBe("67906");  // whitespace stripped
    result.Value[0].Street.ShouldBe("Havlíčkovo náměstí");
    result.Value[0].HouseNumber.ShouldBe("71");
  }

  [Fact]
  public async Task SuggestAsync_NonAddressItems_AreFilteredOut()
  {
    const string body = """
      {
        "items": [
          { "type": "regional.municipality", "regionalStructure": [] },
          { "type": "regional.street", "regionalStructure": [] },
          { "type": "regional.address", "zip": "12345",
            "regionalStructure": [
              { "name": "Germany", "type": "regional.country", "isoCode": "DE" }
            ]
          }
        ]
      }
      """;

    using var handler = new StubHttpMessageHandler(HttpStatusCode.OK, body);
    using HttpClient client = BuildClient(handler);
    MapyAddressSuggester sut = BuildSut(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("Germany", 5, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldHaveSingleItem();
    result.Value[0].CountryCode.ShouldBe("DE");
  }

  [Fact]
  public async Task SuggestAsync_MissingFields_ProduceEmptyStrings()
  {
    const string body = """
      {
        "items": [
          { "type": "regional.address", "regionalStructure": [] }
        ]
      }
      """;

    using var handler = new StubHttpMessageHandler(HttpStatusCode.OK, body);
    using HttpClient client = BuildClient(handler);
    MapyAddressSuggester sut = BuildSut(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("Anything", 5, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldHaveSingleItem();
    result.Value[0].CountryCode.ShouldBe(string.Empty);
    result.Value[0].City.ShouldBe(string.Empty);
    result.Value[0].ZipCode.ShouldBe(string.Empty);
    result.Value[0].Street.ShouldBe(string.Empty);
    result.Value[0].HouseNumber.ShouldBe(string.Empty);
  }

  [Fact]
  public async Task SuggestAsync_NoMunicipality_FallsBackToLocationBeforeComma()
  {
    const string body = """
      {
        "items": [
          {
            "type": "regional.address",
            "location": "Berlin, Deutschland",
            "regionalStructure": [
              { "name": "Germany", "type": "regional.country", "isoCode": "DE" }
            ]
          }
        ]
      }
      """;

    using var handler = new StubHttpMessageHandler(HttpStatusCode.OK, body);
    using HttpClient client = BuildClient(handler);
    MapyAddressSuggester sut = BuildSut(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("Berlin", 5, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldHaveSingleItem();
    result.Value[0].City.ShouldBe("Berlin");
  }

  [Fact]
  public async Task SuggestAsync_NullItems_ReturnsEmptySuccess()
  {
    using var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
    using HttpClient client = BuildClient(handler);
    MapyAddressSuggester sut = BuildSut(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("Anything", 5, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBeEmpty();
  }

  [Fact]
  public async Task SuggestAsync_HttpError_ReturnsProviderUnavailable()
  {
    using var handler = new StubHttpMessageHandler(HttpStatusCode.BadGateway, "");
    using HttpClient client = BuildClient(handler);
    MapyAddressSuggester sut = BuildSut(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("Anything", 5, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(AddressErrors.ProviderUnavailable);
  }

  [Fact]
  public async Task SuggestAsync_JsonException_ReturnsProviderUnavailable()
  {
    using var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "not json");
    using HttpClient client = BuildClient(handler);
    MapyAddressSuggester sut = BuildSut(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("Anything", 5, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(AddressErrors.ProviderUnavailable);
  }

  [Fact]
  public async Task SuggestAsync_UrlEncodesQueryAndApiKey()
  {
    using var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
    using HttpClient client = BuildClient(handler);
    MapyAddressSuggester sut = BuildSut(client, apiKey: "a&b+c");

    await sut.SuggestAsync("Havlíčkovo náměstí", 3, CancellationToken.None);

    handler.LastRequestUri.ShouldNotBeNull();
    string fullUrl = handler.LastRequestUri.AbsoluteUri;
    fullUrl.ShouldContain("query=Havl");
    fullUrl.ShouldContain("apikey=a%26b%2Bc");  // & and + encoded
    fullUrl.ShouldContain("limit=3");
    fullUrl.ShouldContain("lang=cs");
  }
}
