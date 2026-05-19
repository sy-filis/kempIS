using Application.Abstractions.Authentication;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.ReservationStates;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Web.Api.IntegrationTests.Stats;

public sealed class GetOccupancyStatsEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public GetOccupancyStatsEndpointTests(ApiFactory factory) => _factory = factory;

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
  public async Task Get_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(
      new Uri("stats/occupancy?from=2026-07-01&to=2026-07-10", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Get_WrongRole_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.CleaningStaff).GetAsync(
      new Uri("stats/occupancy?from=2026-07-01&to=2026-07-10", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Get_ToBeforeFrom_Returns400()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("stats/occupancy?from=2026-08-01&to=2026-07-01", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Get_RangeTooLarge_Returns400()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("stats/occupancy?from=2026-01-01&to=2027-01-02", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Get_HappyPath_ReturnsGroupedOccupancy()
  {
    var sgId = Guid.NewGuid();
    DomainReservation r = new ReservationBuilder()
      .WithId(Guid.NewGuid())
      .For(new DateOnly(2026, 7, 3), new DateOnly(2026, 7, 7))
      .InState(ReservationState.Confirmed).Build();
    Guid reservationId = r.Id;

    await _factory.WithDbAsync(async db =>
    {
      db.SpotGroups.Add(new SpotGroupBuilder().WithId(sgId).WithName("Mobile").WithCapacity(5).Build());
      db.Reservations.Add(r);
      db.ReservationSpotItems.Add(new ReservationSpotItem
      {
        Id = Guid.NewGuid(),
        ReservationId = reservationId,
        SpotGroupId = sgId,
        SpotId = null,
        HasGivenKey = false,
        HasReturnedKeys = false,
      });
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Manager).GetAsync(
      new Uri("stats/occupancy?from=2026-07-01&to=2026-07-10", UriKind.Relative));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    OccupancyStatsDto? body = await response.Content.ReadFromJsonAsync<OccupancyStatsDto>();
    body.ShouldNotBeNull();
    body.NightsInRange.ShouldBe(10);
    body.Groups.Count.ShouldBe(1);
    body.Groups[0].Name.ShouldBe("Mobile");
    body.Groups[0].OccupiedSpotNights.ShouldBe(4);
    body.Groups[0].CapacitySpotNights.ShouldBe(50);
  }

  private sealed record OccupancyStatsDto(
    DateOnly From, DateOnly To, int NightsInRange,
    int TotalOccupiedSpotNights, int TotalCapacitySpotNights,
    decimal TotalOccupancyPercent,
    IReadOnlyList<OccupancyStatsRowDto> Groups);

  private sealed record OccupancyStatsRowDto(
    Guid SpotGroupId, string Name, bool IsActive, uint Capacity,
    int OccupiedSpotNights, int CapacitySpotNights, decimal OccupancyPercent);
}
