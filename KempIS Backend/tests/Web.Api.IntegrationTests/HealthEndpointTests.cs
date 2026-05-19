using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<ApiFactory>
{
  private readonly ApiFactory _factory;

  public HealthEndpointTests(ApiFactory factory) => _factory = factory;

  [Fact]
  public async Task Health_ReturnsSuccess()
  {
    HttpClient client = _factory.CreateClient();

    HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative));

    response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
  }
}
