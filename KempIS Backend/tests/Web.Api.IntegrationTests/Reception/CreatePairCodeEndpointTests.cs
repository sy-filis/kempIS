using Application.Abstractions.Authentication;
using Application.Reception.PairCodes.Commands.CreatePairCode;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reception;

public sealed class CreatePairCodeEndpointTests : IClassFixture<ApiFactory>
{
  private readonly ApiFactory _factory;

  public CreatePairCodeEndpointTests(ApiFactory factory) => _factory = factory;

  private HttpClient Client(params string[] roles)
  {
    HttpClient client = _factory.CreateClient();
    if (roles.Length > 0)
    {
      client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
    }
    return client;
  }

  [Fact]
  public async Task CreatePairCode_Anonymous_Returns401()
  {
    HttpResponseMessage r = await Client().PostAsync(new Uri("reception/pair-codes", UriKind.Relative), content: null);
    r.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task CreatePairCode_AsCleaningStaff_Returns403()
  {
    HttpResponseMessage r = await Client(Roles.CleaningStaff).PostAsync(
      new Uri("reception/pair-codes", UriKind.Relative), content: null);
    r.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task CreatePairCode_AsReceptionist_Returns200WithPairCodeAndExpiry()
  {
    HttpResponseMessage r = await Client(Roles.Receptionist).PostAsync(
      new Uri("reception/pair-codes", UriKind.Relative), content: null);

    r.StatusCode.ShouldBe(HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    CreatePairCodeResponse? body = await r.Content.ReadFromJsonAsync<CreatePairCodeResponse>();
    body.ShouldNotBeNull();
    body.PairCode.ShouldNotBeNullOrEmpty();
    body.PairCode.ShouldMatch("^[A-Za-z0-9_-]+$");
    body.ExpiresAtUtc.ShouldBeGreaterThan(DateTime.UtcNow);
  }
}
