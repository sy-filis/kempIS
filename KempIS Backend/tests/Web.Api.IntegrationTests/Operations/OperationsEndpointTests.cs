using Application.Abstractions.Authentication;
using Application.Operations.OutOfOrders;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Operations;

public sealed class OutOfOrdersEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public OutOfOrdersEndpointTests(ApiFactory factory) => _factory = factory;

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

  private static object Request(string reason = "maintenance") => new
  {
    From = new DateOnly(2026, 6, 1),
    To = new DateOnly(2026, 6, 3),
    Reason = reason,
    SpotGroupIds = new[] { Guid.NewGuid() },
    SpotIds = Array.Empty<Guid>(),
  };

  [Fact]
  public async Task Crud_Manager_Roundtrip_Succeeds()
  {
    HttpClient client = Client(Roles.Manager);

    HttpResponseMessage postResponse = await client.PostAsJsonAsync(
      new Uri("out-of-orders", UriKind.Relative), Request());
    postResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    Guid id = await postResponse.Content.ReadFromJsonAsync<Guid>();

    HttpResponseMessage getResponse = await client.GetAsync(new Uri("out-of-orders", UriKind.Relative));
    getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

    HttpResponseMessage putResponse = await client.PutAsJsonAsync(
      new Uri($"out-of-orders/{id}", UriKind.Relative), Request(reason: "deep-clean"));
    putResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    HttpResponseMessage deleteResponse = await client.DeleteAsync(new Uri($"out-of-orders/{id}", UriKind.Relative));
    deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
  }

  [Fact]
  public async Task GetOutOfOrders_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(new Uri("out-of-orders", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GetOutOfOrders_CleaningStaff_Returns200()
  {
    HttpResponseMessage response = await Client(Roles.CleaningStaff).GetAsync(new Uri("out-of-orders", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
  }

  [Fact]
  public async Task PostOutOfOrder_CleaningStaff_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.CleaningStaff).PostAsJsonAsync(
      new Uri("out-of-orders", UriKind.Relative), Request());
    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task GetOutOfOrders_FromAndToFilter_ReturnsOnlyOverlapping()
  {
    HttpClient client = Client(Roles.Manager);

    _ = await PostBlockAsync(client,
      new DateOnly(2026, 6, 1),
      new DateOnly(2026, 6, 5));
    Guid overlappingId = await PostBlockAsync(client,
      new DateOnly(2026, 6, 8),
      new DateOnly(2026, 6, 12));
    _ = await PostBlockAsync(client,
      new DateOnly(2026, 6, 20),
      new DateOnly(2026, 6, 25));

    var requestUri = new Uri(
      "out-of-orders?from=2026-06-10&to=2026-06-15",
      UriKind.Relative);
    HttpResponseMessage response = await client.GetAsync(requestUri);
    response.StatusCode.ShouldBe(HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    List<OutOfOrderResponse>? body = await response.Content.ReadFromJsonAsync<List<OutOfOrderResponse>>();
    body.ShouldNotBeNull();
    body.Select(o => o.Id).ShouldBe([overlappingId], ignoreOrder: true);
  }

  [Fact]
  public async Task GetOutOfOrders_OnlyFromFilter_ReturnsBlocksEndingAtOrAfterFrom()
  {
    HttpClient client = Client(Roles.Manager);

    _ = await PostBlockAsync(client,
      new DateOnly(2026, 6, 1),
      new DateOnly(2026, 6, 5));
    Guid afterId = await PostBlockAsync(client,
      new DateOnly(2026, 6, 20),
      new DateOnly(2026, 6, 25));

    var requestUri = new Uri("out-of-orders?from=2026-06-10", UriKind.Relative);
    HttpResponseMessage response = await client.GetAsync(requestUri);
    response.StatusCode.ShouldBe(HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    List<OutOfOrderResponse>? body = await response.Content.ReadFromJsonAsync<List<OutOfOrderResponse>>();
    body.ShouldNotBeNull();
    body.Select(o => o.Id).ShouldBe([afterId], ignoreOrder: true);
  }

  private static async Task<Guid> PostBlockAsync(HttpClient client, DateOnly from, DateOnly to)
  {
    object payload = new
    {
      From = from,
      To = to,
      Reason = "maintenance",
      SpotGroupIds = new[] { Guid.NewGuid() },
      SpotIds = Array.Empty<Guid>(),
    };

    HttpResponseMessage response = await client.PostAsJsonAsync(
      new Uri("out-of-orders", UriKind.Relative), payload);
    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    return await response.Content.ReadFromJsonAsync<Guid>();
  }
}

public sealed class EventsEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public EventsEndpointTests(ApiFactory factory) => _factory = factory;

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

  private static object Request(string name = "Summer Festival") => new
  {
    Name = name,
    Description = (string?)null,
    StartsAt = new DateOnly(2026, 8, 1),
    EndsAt = (DateOnly?)new DateOnly(2026, 8, 3),
    SpotGroupIds = new[] { Guid.NewGuid() },
  };

  [Fact]
  public async Task Crud_Manager_Roundtrip_Succeeds()
  {
    HttpClient client = Client(Roles.Manager);

    HttpResponseMessage postResponse = await client.PostAsJsonAsync(
      new Uri("events", UriKind.Relative), Request());
    postResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    Guid id = await postResponse.Content.ReadFromJsonAsync<Guid>();

    HttpResponseMessage getResponse = await client.GetAsync(new Uri("events", UriKind.Relative));
    getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

    HttpResponseMessage putResponse = await client.PutAsJsonAsync(
      new Uri($"events/{id}", UriKind.Relative), Request(name: "Autumn Festival"));
    putResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    HttpResponseMessage deleteResponse = await client.DeleteAsync(new Uri($"events/{id}", UriKind.Relative));
    deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
  }

  [Fact]
  public async Task GetEvents_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(new Uri("events", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task PostEvent_Receptionist_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("events", UriKind.Relative), Request());
    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task PostEvent_EmptySpotGroupIds_Returns400()
  {
    object payload = new
    {
      Name = "No groups",
      Description = (string?)null,
      StartsAt = new DateOnly(2026, 8, 1),
      EndsAt = (DateOnly?)new DateOnly(2026, 8, 3),
      SpotGroupIds = Array.Empty<Guid>(),
    };

    HttpResponseMessage response = await Client(Roles.Manager).PostAsJsonAsync(
      new Uri("events", UriKind.Relative), payload);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task PutEvent_EmptySpotGroupIds_Returns400()
  {
    HttpClient client = Client(Roles.Manager);

    HttpResponseMessage postResponse = await client.PostAsJsonAsync(
      new Uri("events", UriKind.Relative), Request());
    postResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    Guid id = await postResponse.Content.ReadFromJsonAsync<Guid>();

    object payload = new
    {
      Name = "No groups",
      Description = (string?)null,
      StartsAt = new DateOnly(2026, 8, 1),
      EndsAt = (DateOnly?)new DateOnly(2026, 8, 3),
      SpotGroupIds = Array.Empty<Guid>(),
    };

    HttpResponseMessage putResponse = await client.PutAsJsonAsync(
      new Uri($"events/{id}", UriKind.Relative), payload);

    putResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }
}
