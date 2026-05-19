using Application.Abstractions.Authentication;
using Application.Finance.LegalEntities.Queries.GetLegalEntityFromAres;
using Domain.Finance.LegalEntities;
using SharedKernel;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Finance;

public sealed class GetLegalEntityFromAresEndpointTests : IClassFixture<ApiFactory>
{
  private readonly ApiFactory _factory;

  public GetLegalEntityFromAresEndpointTests(ApiFactory factory) => _factory = factory;

  private HttpClient Client(params string[] roles)
  {
    HttpClient client = _factory.CreateClient();
    if (roles.Length > 0)
    {
      client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
    }
    return client;
  }

  private static LegalEntityFinderResponse SampleResponse() => new(
      Name: "OLŠOVEC s.r.o.",
      Cin: "60709448",
      Tin: "CZ60709448",
      Address: new AresAddressResponse(
        CountryCode: "CZ",
        City: "Jedovnice",
        ZipCode: "67906",
        Street: "Havlíčkovo náměstí",
        HouseNumber: "71"));

  [Fact]
  public async Task Get_Anonymous_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(
      new Uri("legal-entities/ares/60709448", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Get_AsAccountant_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.Accountant).GetAsync(
      new Uri("legal-entities/ares/60709448", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Get_WithInvalidCinFormat_Returns400()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("legal-entities/ares/abc", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Get_AsReceptionist_WithKnownCin_Returns200AndMappedPayload()
  {
    _factory.LegalEntityFinder
      .FindByCinAsync("60709448", Arg.Any<CancellationToken>())
      .Returns(Result.Success(SampleResponse()));

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("legal-entities/ares/60709448", UriKind.Relative));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    LegalEntityFinderResponse? body = await response.Content
      .ReadFromJsonAsync<LegalEntityFinderResponse>();

    body.ShouldNotBeNull();
    body.Name.ShouldBe("OLŠOVEC s.r.o.");
    body.Cin.ShouldBe("60709448");
    body.Tin.ShouldBe("CZ60709448");
    body.Address.CountryCode.ShouldBe("CZ");
    body.Address.City.ShouldBe("Jedovnice");
    body.Address.ZipCode.ShouldBe("67906");
    body.Address.Street.ShouldBe("Havlíčkovo náměstí");
    body.Address.HouseNumber.ShouldBe("71");
  }

  [Fact]
  public async Task Get_AsManager_WithKnownCin_Returns200()
  {
    _factory.LegalEntityFinder
      .FindByCinAsync("60709448", Arg.Any<CancellationToken>())
      .Returns(Result.Success(SampleResponse()));

    HttpResponseMessage response = await Client(Roles.Manager).GetAsync(
      new Uri("legal-entities/ares/60709448", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
  }

  [Fact]
  public async Task Get_WhenFinderReturnsNotFound_Returns404()
  {
    _factory.LegalEntityFinder
      .FindByCinAsync("99999999", Arg.Any<CancellationToken>())
      .Returns(Result.Failure<LegalEntityFinderResponse>(LegalEntityErrors.NotFoundInAres("99999999")));

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("legal-entities/ares/99999999", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Get_WhenFinderReturnsAresUnavailable_Returns500()
  {
    _factory.LegalEntityFinder
      .FindByCinAsync("60709448", Arg.Any<CancellationToken>())
      .Returns(Result.Failure<LegalEntityFinderResponse>(LegalEntityErrors.AresUnavailable));

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("legal-entities/ares/60709448", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
  }
}
