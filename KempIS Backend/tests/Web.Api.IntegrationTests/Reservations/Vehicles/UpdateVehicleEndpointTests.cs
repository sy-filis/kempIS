using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Authentication;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reservations.Vehicles;

public sealed class UpdateVehicleEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public UpdateVehicleEndpointTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync() => _factory.ResetAllAsync();
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

  [Fact]
  public async Task Put_WithAllForeignKeysNull_Returns204()
  {
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage createResponse = await client.PostAsJsonAsync("vehicles", new
    {
      reservationId = (Guid?)null,
      billId = (Guid?)null,
      serviceId = (Guid?)null,
      registrationNumber = "AB1234",
    });
    string createError = _factory.ServerExceptions.TryPeek(out Exception? cex) ? cex.ToString() : "no exception";
    createResponse.StatusCode.ShouldBe(HttpStatusCode.Created, createError);
    Guid vehicleId = await createResponse.Content.ReadFromJsonAsync<Guid>();

    HttpResponseMessage putResponse = await client.PutAsJsonAsync($"vehicles/{vehicleId}", new
    {
      reservationId = (Guid?)null,
      billId = (Guid?)null,
      serviceId = (Guid?)null,
      registrationNumber = "CD5678",
    });
    string putError = _factory.ServerExceptions.TryPeek(out Exception? pex) ? pex.ToString() : "no exception";
    putResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent, putError);
  }
}
