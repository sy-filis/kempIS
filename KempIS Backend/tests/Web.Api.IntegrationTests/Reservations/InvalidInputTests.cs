using Application.Abstractions.Authentication;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reservations;

public sealed class InvalidInputTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public InvalidInputTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync() => _factory.ResetReservationsAsync();
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
  public async Task GetAvailability_FromDateMalformed_Returns400()
  {
    HttpResponseMessage response = await Client().GetAsync(
      new Uri("availability?from=2026-13-99&to=2026-05-01", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task PostReservation_FromAsDateTimeString_Returns400()
  {
    var request = new
    {
      Name = "Pat",
      Surname = "Smith",
      Email = "pat@example.com",
      Phone = "+420123456789",
      From = "2026-04-26T10:30:00Z",
      To = "2026-04-30",
      SpotIds = new[] { Guid.NewGuid() },
      Note = (string?)null,
      GroupReservationId = (Guid?)null,
    };

    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("reservations", UriKind.Relative), request);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task PostReservation_SpotIdNotGuid_Returns400()
  {
    var request = new
    {
      Name = "Pat",
      Surname = "Smith",
      Email = "pat@example.com",
      Phone = "+420123456789",
      From = "2026-04-26",
      To = "2026-04-30",
      SpotIds = new[] { "not-a-guid" },
      Note = (string?)null,
      GroupReservationId = (Guid?)null,
    };

    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("reservations", UriKind.Relative), request);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  // 404 because the {id:guid} route constraint does not match a non-Guid segment, so routing
  // never reaches the endpoint handler at all.
  [Fact]
  public async Task PutReservation_RouteIdNotGuid_Returns404()
  {
    var request = new
    {
      From = new DateOnly(2026, 4, 26),
      To = new DateOnly(2026, 4, 30),
      Note = (string?)null,
    };

    HttpResponseMessage response = await Client(Roles.Manager).PutAsJsonAsync(
      new Uri("reservations/not-a-guid", UriKind.Relative), request);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task PutReservation_RouteIdNotFound_Returns404()
  {
    var request = new
    {
      Name = "Jan",
      Surname = "Novak",
      Email = "jan@example.com",
      Phone = "+420",
      From = new DateOnly(2026, 4, 26),
      To = new DateOnly(2026, 4, 30),
      Note = (string?)null,
      GroupReservationId = (Guid?)null,
      SpotIds = new[] { Guid.NewGuid() },
      Services = Array.Empty<object>(),
      Vehicles = Array.Empty<object>(),
    };

    HttpResponseMessage response = await Client(Roles.Manager).PutAsJsonAsync(
      new Uri($"reservations/{Guid.NewGuid()}", UriKind.Relative), request);

    string err = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.NotFound, err);
  }

  [Fact]
  public async Task PostReservation_SpotIdNotFound_Returns404()
  {
    var request = new
    {
      Name = "Pat",
      Surname = "Smith",
      Email = "pat@example.com",
      Phone = "+420123456789",
      From = new DateOnly(2026, 4, 26),
      To = new DateOnly(2026, 4, 30),
      SpotIds = new[] { Guid.NewGuid() },
      Note = (string?)null,
      GroupReservationId = (Guid?)null,
    };

    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("reservations", UriKind.Relative), request);

    string err = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.NotFound, err);
  }
}
