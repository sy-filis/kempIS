using Application.Reservations.Queries.GetAvailability;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reservations;

public sealed class AvailabilityEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;
  private readonly HttpClient _client;

  public AvailabilityEndpointTests(ApiFactory factory)
  {
    _factory = factory;
    _client = factory.CreateClient();
  }

  public Task InitializeAsync() => _factory.ResetReservationsAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task Get_Anonymous_Returns200WithEmptyAvailability()
  {
    HttpResponseMessage response = await _client.GetAsync(
      new Uri("availability?from=2026-07-10&to=2026-07-15", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    AvailabilityResponse? body = await response.Content.ReadFromJsonAsync<AvailabilityResponse>();
    body.ShouldNotBeNull();
    body.SpotGroups.ShouldBeEmpty();
  }

  [Fact]
  public async Task Get_WithSeededSpotGroup_ReturnsCapacityAndZeroOccupied()
  {
    var groupId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.SpotGroups.Add(new SpotGroupBuilder().WithId(groupId).WithCapacity(5).WithName("Site A").Build());
      for (int i = 0; i < 5; i++)
      {
        db.Spots.Add(new SpotBuilder().InGroup(groupId).Build());
      }
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await _client.GetAsync(
      new Uri("availability?from=2026-07-10&to=2026-07-15", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    AvailabilityResponse body = (await response.Content.ReadFromJsonAsync<AvailabilityResponse>())!;
    SpotGroupAvailability row = body.SpotGroups.Single(g => g.SpotGroupId == groupId);
    row.Capacity.ShouldBe(5u);
    row.TotalSpots.ShouldBe(5);
    row.Occupied.ShouldBe(0);
    row.Available.ShouldBe(5);
  }

}
