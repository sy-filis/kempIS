using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Authentication;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reservations.Vehicles;

public sealed class CreateVehicleEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public CreateVehicleEndpointTests(ApiFactory factory) => _factory = factory;

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
  public async Task Post_WithOnlyRegistrationNumber_Returns201()
  {
    HttpClient client = Client(Roles.Receptionist);
    var body = new
    {
      reservationId = (Guid?)null,
      billId = (Guid?)null,
      serviceId = (Guid?)null,
      registrationNumber = "ABC123",
    };

    HttpResponseMessage response = await client.PostAsJsonAsync("vehicles", body);
    string error = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.Created, error);
  }

  [Fact]
  public async Task Post_WithEmptyRegistrationNumber_Returns400()
  {
    HttpClient client = Client(Roles.Receptionist);
    var body = new
    {
      reservationId = (Guid?)null,
      billId = (Guid?)null,
      serviceId = (Guid?)null,
      registrationNumber = "",
    };

    HttpResponseMessage response = await client.PostAsJsonAsync("vehicles", body);
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Post_WithRegistrationNumberOver20Chars_Returns400()
  {
    HttpClient client = Client(Roles.Receptionist);
    var body = new
    {
      reservationId = (Guid?)null,
      billId = (Guid?)null,
      serviceId = (Guid?)null,
      registrationNumber = new string('X', 21),
    };

    HttpResponseMessage response = await client.PostAsJsonAsync("vehicles", body);
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }
}
