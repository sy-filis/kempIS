using System.Net;
using Application.Abstractions.Authentication;
using Domain.Reservations.Reservations;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.ReservationStates;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reservations.ReservationSpotItems;

public sealed class GiveKeyEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public GiveKeyEndpointTests(ApiFactory factory) => _factory = factory;

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

  private async Task<Guid> SeedSpotItemAsync(ReservationState state, bool hasGivenKey = false)
  {
    var spotItemId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      Reservation reservation = new ReservationBuilder().InState(state).Build();
      db.Reservations.Add(reservation);

      var spotItem = new ReservationSpotItem
      {
        Id = spotItemId,
        ReservationId = reservation.Id,
        SpotGroupId = Guid.NewGuid(),
        SpotId = Guid.NewGuid(),
        HasGivenKey = hasGivenKey,
      };
      db.ReservationSpotItems.Add(spotItem);
      await db.SaveChangesAsync();
    });
    return spotItemId;
  }

  private async Task<bool> ReadHasGivenKeyAsync(Guid spotItemId)
  {
    bool result = false;
    await _factory.WithDbAsync(async db =>
    {
      ReservationSpotItem item = (await db.ReservationSpotItems.AsNoTracking().SingleAsync(x => x.Id == spotItemId))!;
      result = item.HasGivenKey;
    });
    return result;
  }

  [Fact]
  public async Task GiveKey_OnConfirmedReservation_Returns204AndFlipsFlag()
  {
    Guid spotItemId = await SeedSpotItemAsync(ReservationState.Confirmed);
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage response = await client.PostAsync(
      new Uri($"reservation-spot-items/{spotItemId}/give-key", UriKind.Relative), content: null);

    string err = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.NoContent, err);
    (await ReadHasGivenKeyAsync(spotItemId)).ShouldBeTrue();
  }

  [Fact]
  public async Task GiveKey_OnCheckedInReservation_Returns204()
  {
    Guid spotItemId = await SeedSpotItemAsync(ReservationState.CheckedIn);
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage response = await client.PostAsync(
      new Uri($"reservation-spot-items/{spotItemId}/give-key", UriKind.Relative), content: null);

    string err = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.NoContent, err);
  }

  [Fact]
  public async Task GiveKey_OnCompletedReservation_Returns400()
  {
    Guid spotItemId = await SeedSpotItemAsync(ReservationState.Completed);
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage response = await client.PostAsync(
      new Uri($"reservation-spot-items/{spotItemId}/give-key", UriKind.Relative), content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task GiveKey_NonExistentSpotItem_Returns404()
  {
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage response = await client.PostAsync(
      new Uri($"reservation-spot-items/{Guid.NewGuid()}/give-key", UriKind.Relative), content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task GiveKey_TwiceIsIdempotent_BothReturn204()
  {
    Guid spotItemId = await SeedSpotItemAsync(ReservationState.Confirmed);
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage first = await client.PostAsync(
      new Uri($"reservation-spot-items/{spotItemId}/give-key", UriKind.Relative), content: null);
    HttpResponseMessage second = await client.PostAsync(
      new Uri($"reservation-spot-items/{spotItemId}/give-key", UriKind.Relative), content: null);

    first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
  }

  [Fact]
  public async Task GiveKey_AsAnonymous_Returns401()
  {
    Guid spotItemId = await SeedSpotItemAsync(ReservationState.Confirmed);
    HttpClient client = _factory.CreateClient();

    HttpResponseMessage response = await client.PostAsync(
      new Uri($"reservation-spot-items/{spotItemId}/give-key", UriKind.Relative), content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GiveKey_AsAccountant_Returns403()
  {
    Guid spotItemId = await SeedSpotItemAsync(ReservationState.Confirmed);
    HttpClient client = Client(Roles.Accountant);

    HttpResponseMessage response = await client.PostAsync(
      new Uri($"reservation-spot-items/{spotItemId}/give-key", UriKind.Relative), content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }
}
