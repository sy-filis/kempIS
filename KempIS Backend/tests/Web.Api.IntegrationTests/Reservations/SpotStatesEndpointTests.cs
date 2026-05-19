using Application.Abstractions.Authentication;
using Application.Reservations.Spots;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.ReservationStates;
using Domain.Reservations.SpotGroups;
using Domain.Reservations.Spots;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Web.Api.IntegrationTests.Reservations;

public sealed class SpotStatesEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public SpotStatesEndpointTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync() => _factory.ResetAllAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  private HttpClient Client(params string[] roles)
  {
    HttpClient c = _factory.CreateClient();
    if (roles.Length > 0)
    {
      c.DefaultRequestHeaders.Add("X-Test-Roles", string.Join(",", roles));
    }
    return c;
  }

  private async Task<(SpotGroup, Spot)> SeedActiveSpot()
  {
    SpotGroup g = new SpotGroupBuilder().Build();
    Spot s = new()
    {
      Id = Guid.NewGuid(),
      SpotGroupId = g.Id,
      Name = "S",
      IsActive = true,
    };
    await _factory.WithDbAsync(async db =>
    {
      db.SpotGroups.Add(g);
      db.Spots.Add(s);
      await db.SaveChangesAsync();
    });
    return (g, s);
  }

  [Fact]
  public async Task GetSpotStates_AsAuthenticated_Returns200()
  {
    (_, Spot s) = await SeedActiveSpot();

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("spots/states", UriKind.Relative));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    List<SpotStateResponse>? body = await response.Content.ReadFromJsonAsync<List<SpotStateResponse>>();
    body.ShouldNotBeNull();
    body.ShouldContain(r => r.SpotId == s.Id);
  }

  [Fact]
  public async Task GetSpotStates_Anonymous_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(new Uri("spots/states", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GetSpotStates_OmitsInactiveSpots()
  {
    SpotGroup g = new SpotGroupBuilder().Build();
    Spot inactive = new()
    {
      Id = Guid.NewGuid(),
      SpotGroupId = g.Id,
      Name = "S-inactive",
      IsActive = false,
    };
    await _factory.WithDbAsync(async db =>
    {
      db.SpotGroups.Add(g);
      db.Spots.Add(inactive);
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("spots/states", UriKind.Relative));
    List<SpotStateResponse>? body = await response.Content.ReadFromJsonAsync<List<SpotStateResponse>>();

    body!.ShouldNotContain(r => r.SpotId == inactive.Id);
  }

  [Fact]
  public async Task GetSpotStates_BackToBack_PrefersCheckedIn()
  {
    (SpotGroup g, Spot s) = await SeedActiveSpot();
    DateTime nowUtc = DateTime.UtcNow;
    var today = DateOnly.FromDateTime(nowUtc);

    DomainReservation departing = new ReservationBuilder()
      .InState(ReservationState.CheckedIn)
      .For(today.AddDays(-2), today)
      .Build();
    DomainReservation arriving = new ReservationBuilder()
      .InState(ReservationState.Confirmed)
      .For(today, today.AddDays(3))
      .Build();
    ReservationSpotItem departingItem = new()
    {
      Id = Guid.NewGuid(),
      ReservationId = departing.Id,
      SpotGroupId = g.Id,
      SpotId = s.Id,
      HasReturnedKeys = false,
    };
    ReservationSpotItem arrivingItem = new()
    {
      Id = Guid.NewGuid(),
      ReservationId = arriving.Id,
      SpotGroupId = g.Id,
      SpotId = s.Id,
      HasReturnedKeys = false,
    };
    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(departing);
      db.Reservations.Add(arriving);
      db.ReservationSpotItems.Add(departingItem);
      db.ReservationSpotItems.Add(arrivingItem);
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("spots/states", UriKind.Relative));
    List<SpotStateResponse>? body = await response.Content.ReadFromJsonAsync<List<SpotStateResponse>>();

    SpotStateResponse row = body!.Single(r => r.SpotId == s.Id);
    row.State.ShouldBe(SpotState.ExpectingDeparture);
    row.DepartureDate.ShouldBe(today);
  }

  [Fact]
  public async Task Get_ReturnsHasGivenKeyAndIsPaid_OnEveryRow()
  {
    await SeedActiveSpot();

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("spots/states", UriKind.Relative));
    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    System.Text.Json.JsonElement[]? rows =
      await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement[]>();
    rows.ShouldNotBeNull();

    foreach (System.Text.Json.JsonElement row in rows)
    {
      row.TryGetProperty("hasGivenKey", out _).ShouldBeTrue();
      row.TryGetProperty("isPaid", out _).ShouldBeTrue();
    }
  }
}
