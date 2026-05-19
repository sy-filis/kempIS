using Application.Abstractions.Authentication;
using Domain.Services.Services;
using Domain.Services.ServiceTypes;
using Domain.Services.VatRates;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reservations;

public sealed class SpotGroupsEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public SpotGroupsEndpointTests(ApiFactory factory) => _factory = factory;

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

  private static object CreateRequest(Guid? serviceId = null, string name = "A-Block", uint capacity = 10) => new
  {
    ServiceId = serviceId ?? Guid.NewGuid(),
    Name = name,
    Description = (string?)null,
    Capacity = capacity,
    IsActive = true,
    ImageUrl = "https://example.test/image.png",
    DetailsUrl = "https://example.test/details",
  };

  [Fact]
  public async Task GetSpotGroups_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(new Uri("spot-groups", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task PostSpotGroup_Receptionist_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("spot-groups", UriKind.Relative), CreateRequest());
    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Crud_Manager_Roundtrip_Succeeds()
  {
    HttpClient client = Client(Roles.Manager);

    var serviceId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      var typeId = Guid.NewGuid();
      var vatId = Guid.NewGuid();
      db.ServiceTypes.Add(new ServiceType { Id = typeId, Name = "Accommodation", IsActive = true });
      db.VatRates.Add(new VatRate { Id = vatId, Name = "Standard", Rate = 21m, IsActive = true });
      db.Services.Add(new Service
      {
        Id = serviceId,
        ServiceGroup = ServiceGroup.Spots,
        ServiceTypeId = typeId,
        VatRateId = vatId,
        Name = "Pitch",
        BasePrice = 100m,
        IsActive = true,
      });
      await db.SaveChangesAsync();
    });

    HttpResponseMessage postResponse = await client.PostAsJsonAsync(
      new Uri("spot-groups", UriKind.Relative), CreateRequest(serviceId: serviceId, name: "B-Block", capacity: 12));
    postResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    Guid id = await postResponse.Content.ReadFromJsonAsync<Guid>();

    HttpResponseMessage getResponse = await client.GetAsync(new Uri("spot-groups", UriKind.Relative));
    getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await getResponse.Content.ReadAsStringAsync()).ShouldContain(id.ToString());

    HttpResponseMessage putResponse = await client.PutAsJsonAsync(
      new Uri($"spot-groups/{id}", UriKind.Relative), CreateRequest(serviceId: serviceId, name: "B-Block-Renamed", capacity: 20));
    putResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    HttpResponseMessage deleteResponse = await client.DeleteAsync(new Uri($"spot-groups/{id}", UriKind.Relative));
    deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    await _factory.WithDbAsync(async db =>
    {
      (await db.SpotGroups.CountAsync(sg => sg.Id == id)).ShouldBe(0);
    });
  }

  [Fact]
  public async Task PutSpotGroup_Missing_Manager_Returns404()
  {
    HttpResponseMessage response = await Client(Roles.Manager).PutAsJsonAsync(
      new Uri($"spot-groups/{Guid.NewGuid()}", UriKind.Relative), CreateRequest());
    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task DeleteSpotGroup_Missing_Manager_Returns404()
  {
    HttpResponseMessage response = await Client(Roles.Manager).DeleteAsync(
      new Uri($"spot-groups/{Guid.NewGuid()}", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task PostSpotGroup_InvalidPayload_Manager_Returns400()
  {
    HttpResponseMessage response = await Client(Roles.Manager).PostAsJsonAsync(
      new Uri("spot-groups", UriKind.Relative), new
      {
        ServiceId = Guid.Empty,
        Name = "",
        Description = (string?)null,
        Capacity = 0u,
        IsActive = true,
      });
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }
}

public sealed class SpotsEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public SpotsEndpointTests(ApiFactory factory) => _factory = factory;

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
  public async Task Crud_Manager_Roundtrip_Succeeds()
  {
    var groupId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.SpotGroups.Add(new SpotGroupBuilder().WithId(groupId).Build());
      await db.SaveChangesAsync();
    });

    HttpClient client = Client(Roles.Manager);

    HttpResponseMessage postResponse = await client.PostAsJsonAsync(
      new Uri("spots", UriKind.Relative), new
      {
        SpotGroupId = groupId,
        Name = "S-01",
        Description = (string?)null,
        IsActive = true,
      });
    postResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    Guid spotId = await postResponse.Content.ReadFromJsonAsync<Guid>();

    HttpResponseMessage getResponse = await client.GetAsync(new Uri("spots", UriKind.Relative));
    getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await getResponse.Content.ReadAsStringAsync()).ShouldContain(spotId.ToString());

    HttpResponseMessage putResponse = await client.PutAsJsonAsync(
      new Uri($"spots/{spotId}", UriKind.Relative), new
      {
        SpotGroupId = groupId,
        Name = "S-01-renamed",
        Description = "desc",
        IsActive = false,
      });
    putResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    HttpResponseMessage deleteResponse = await client.DeleteAsync(new Uri($"spots/{spotId}", UriKind.Relative));
    deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
  }

  [Fact]
  public async Task GetSpots_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(new Uri("spots", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task PostSpot_Receptionist_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("spots", UriKind.Relative), new
      {
        SpotGroupId = Guid.NewGuid(),
        Name = "X",
        Description = (string?)null,
        IsActive = true,
      });
    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }
}
