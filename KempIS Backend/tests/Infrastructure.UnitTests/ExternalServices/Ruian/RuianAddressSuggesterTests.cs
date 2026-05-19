using System.Net;
using Domain.Common;
using Infrastructure.ExternalServices.Ruian;
using Infrastructure.UnitTests.TestDoubles;
using SharedKernel;

namespace Infrastructure.UnitTests.ExternalServices.Ruian;

public sealed class RuianAddressSuggesterTests
{
  private const string BaseUrl = "https://ruian.test/geocode/";

  private static HttpClient BuildClient(StubHttpMessageHandler handler) =>
    new(handler)
    {
      BaseAddress = new Uri(BaseUrl),
    };

  [Fact]
  public async Task SuggestAsync_WithSuccessfulResponse_MapsCandidatesToSuggestions()
  {
    const string body = """
      {
        "candidates": [
          {
            "address": "Havlíčkovo náměstí 71, 67906 Jedovnice",
            "score": 100
          }
        ]
      }
      """;

    using var handler = new StubHttpMessageHandler(HttpStatusCode.OK, body);
    using HttpClient client = BuildClient(handler);
    var sut = new RuianAddressSuggester(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("Havlíčkovo", 5, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldHaveSingleItem();
    result.Value[0].CountryCode.ShouldBe("CZ");
    result.Value[0].Street.ShouldBe("Havlíčkovo náměstí");
    result.Value[0].HouseNumber.ShouldBe("71");
    result.Value[0].ZipCode.ShouldBe("67906");
    result.Value[0].City.ShouldBe("Jedovnice");
  }

  [Fact]
  public async Task SuggestAsync_AddressUsesCpPlaceholder_StreetFallsBackToCity()
  {
    const string body = """
      {
        "candidates": [
          {
            "address": "č.p. 246, 67934 Knínice",
            "score": 100
          }
        ]
      }
      """;

    using var handler = new StubHttpMessageHandler(HttpStatusCode.OK, body);
    using HttpClient client = BuildClient(handler);
    var sut = new RuianAddressSuggester(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("246 knínic", 5, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldHaveSingleItem();
    result.Value[0].CountryCode.ShouldBe("CZ");
    result.Value[0].City.ShouldBe("Knínice");
    result.Value[0].ZipCode.ShouldBe("67934");
    result.Value[0].Street.ShouldBe("Knínice");
    result.Value[0].HouseNumber.ShouldBe("246");
  }

  [Fact]
  public async Task SuggestAsync_FiltersOutLowScoreCandidates()
  {
    const string body = """
      {
        "candidates": [
          { "address": "Low Score Street 1, 12345 Somewhere", "score": 85 },
          { "address": "High Score Street 2, 12345 Somewhere", "score": 95 }
        ]
      }
      """;

    using var handler = new StubHttpMessageHandler(HttpStatusCode.OK, body);
    using HttpClient client = BuildClient(handler);
    var sut = new RuianAddressSuggester(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("Street", 5, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldHaveSingleItem();
    result.Value[0].Street.ShouldBe("High Score Street");
  }

  [Fact]
  public async Task SuggestAsync_SkipsCandidatesThatFailRegex()
  {
    const string body = """
      {
        "candidates": [
          { "address": "unparseable format", "score": 100 },
          { "address": "Good Street 5, 12345 Prague", "score": 100 }
        ]
      }
      """;

    using var handler = new StubHttpMessageHandler(HttpStatusCode.OK, body);
    using HttpClient client = BuildClient(handler);
    var sut = new RuianAddressSuggester(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("Street", 5, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldHaveSingleItem();
    result.Value[0].City.ShouldBe("Prague");
  }

  [Fact]
  public async Task SuggestAsync_LimitsReturnedResultsToRequestedLimit()
  {
    const string body = """
      {
        "candidates": [
          { "address": "A 1, 12345 X", "score": 100 },
          { "address": "B 2, 12345 X", "score": 100 },
          { "address": "C 3, 12345 X", "score": 100 },
          { "address": "D 4, 12345 X", "score": 100 }
        ]
      }
      """;

    using var handler = new StubHttpMessageHandler(HttpStatusCode.OK, body);
    using HttpClient client = BuildClient(handler);
    var sut = new RuianAddressSuggester(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("Street", 2, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(2);
    result.Value[0].Street.ShouldBe("A");
    result.Value[1].Street.ShouldBe("B");
  }

  [Fact]
  public async Task SuggestAsync_EmptyCandidatesArray_ReturnsEmptySuccess()
  {
    using var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{ "candidates": [] }""");
    using HttpClient client = BuildClient(handler);
    var sut = new RuianAddressSuggester(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("Street", 5, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBeEmpty();
  }

  [Fact]
  public async Task SuggestAsync_NullCandidates_ReturnsEmptySuccess()
  {
    using var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
    using HttpClient client = BuildClient(handler);
    var sut = new RuianAddressSuggester(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("Street", 5, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBeEmpty();
  }

  [Fact]
  public async Task SuggestAsync_Returns5xx_ReturnsProviderUnavailable()
  {
    using var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "");
    using HttpClient client = BuildClient(handler);
    var sut = new RuianAddressSuggester(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("Street", 5, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(AddressErrors.ProviderUnavailable);
  }

  [Fact]
  public async Task SuggestAsync_HttpRequestException_ReturnsProviderUnavailable()
  {
    using var handler = StubHttpMessageHandler.Throwing(new HttpRequestException("boom"));
    using HttpClient client = BuildClient(handler);
    var sut = new RuianAddressSuggester(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("Street", 5, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(AddressErrors.ProviderUnavailable);
  }

  [Fact]
  public async Task SuggestAsync_TimeoutTaskCanceled_ReturnsProviderUnavailable()
  {
    using var handler = StubHttpMessageHandler.Throwing(new TaskCanceledException("timeout"));
    using HttpClient client = BuildClient(handler);
    var sut = new RuianAddressSuggester(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("Street", 5, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(AddressErrors.ProviderUnavailable);
  }

  [Fact]
  public async Task SuggestAsync_MalformedJson_ReturnsProviderUnavailable()
  {
    using var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{ not valid json");
    using HttpClient client = BuildClient(handler);
    var sut = new RuianAddressSuggester(client);

    Result<IReadOnlyList<Application.Addresses.Queries.SuggestAddresses.AddressSuggestion>> result =
      await sut.SuggestAsync("Street", 5, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(AddressErrors.ProviderUnavailable);
  }

  [Fact]
  public async Task SuggestAsync_UrlEncodesQuery()
  {
    using var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{ "candidates": [] }""");
    using HttpClient client = BuildClient(handler);
    var sut = new RuianAddressSuggester(client);

    await sut.SuggestAsync("Havlíčkovo náměstí", 5, CancellationToken.None);

    handler.LastRequestUri.ShouldNotBeNull();
    string fullUrl = handler.LastRequestUri.AbsoluteUri;
    fullUrl.ShouldContain("SingleLine=Havl");
    fullUrl.ShouldNotContain(" ");  // space must be encoded
    fullUrl.ShouldContain("f=pjson");
    fullUrl.ShouldContain("outFields=");
  }
}
