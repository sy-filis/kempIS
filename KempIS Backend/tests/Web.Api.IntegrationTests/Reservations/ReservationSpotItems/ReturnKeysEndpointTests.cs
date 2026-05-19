using Application.Abstractions.Authentication;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.ReservationStates;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Web.Api.IntegrationTests.Reservations.ReservationSpotItems;

public sealed class ReturnKeysEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public ReturnKeysEndpointTests(ApiFactory factory) => _factory = factory;

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

  private async Task<(DomainReservation, Domain.Reservations.ReservationSpotItems.ReservationSpotItem)> SeedCheckedInWithItem()
  {
    DomainReservation r = new ReservationBuilder().InState(ReservationState.CheckedIn).Build();
    Domain.Reservations.ReservationSpotItems.ReservationSpotItem item = new()
    {
      Id = Guid.NewGuid(),
      ReservationId = r.Id,
      SpotGroupId = Guid.NewGuid(),
      SpotId = Guid.NewGuid(),
      HasReturnedKeys = false,
    };
    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(r);
      db.ReservationSpotItems.Add(item);
      await db.SaveChangesAsync();
    });
    return (r, item);
  }

  [Fact]
  public async Task ReturnKeys_AsReceptionist_Returns204AndFlagsItem()
  {
    (DomainReservation _, Domain.Reservations.ReservationSpotItems.ReservationSpotItem item) = await SeedCheckedInWithItem();

    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsync(
      new Uri($"reservation-spot-items/{item.Id}/return-keys", UriKind.Relative), content: null);

    response.StatusCode.ShouldBe(
      HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    await _factory.WithDbAsync(async db =>
    {
      Domain.Reservations.ReservationSpotItems.ReservationSpotItem reloaded = await db.ReservationSpotItems.AsNoTracking().SingleAsync(x => x.Id == item.Id);
      reloaded.HasReturnedKeys.ShouldBeTrue();
    });
  }

  [Fact]
  public async Task ReturnKeys_Anonymous_Returns401()
  {
    (DomainReservation _, Domain.Reservations.ReservationSpotItems.ReservationSpotItem item) = await SeedCheckedInWithItem();

    HttpResponseMessage response = await Client().PostAsync(
      new Uri($"reservation-spot-items/{item.Id}/return-keys", UriKind.Relative), content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task ReturnKeys_AsAccountant_Returns403()
  {
    (DomainReservation _, Domain.Reservations.ReservationSpotItems.ReservationSpotItem item) = await SeedCheckedInWithItem();

    HttpResponseMessage response = await Client(Roles.Accountant).PostAsync(
      new Uri($"reservation-spot-items/{item.Id}/return-keys", UriKind.Relative), content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task ReturnKeys_LastItem_FlipsReservationToCompleted()
  {
    (DomainReservation r, Domain.Reservations.ReservationSpotItems.ReservationSpotItem item) = await SeedCheckedInWithItem();

    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsync(
      new Uri($"reservation-spot-items/{item.Id}/return-keys", UriKind.Relative), content: null);

    response.StatusCode.ShouldBe(
      HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    await _factory.WithDbAsync(async db =>
    {
      DomainReservation reloaded = await db.Reservations.AsNoTracking().SingleAsync(x => x.Id == r.Id);
      reloaded.State.ShouldBe(ReservationState.Completed);
    });
  }

  [Fact]
  public async Task ReturnKeys_AlreadyReturned_StaysSuccessful()
  {
    (DomainReservation _, Domain.Reservations.ReservationSpotItems.ReservationSpotItem item) = await SeedCheckedInWithItem();
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage first = await client.PostAsync(
      new Uri($"reservation-spot-items/{item.Id}/return-keys", UriKind.Relative), content: null);
    first.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    HttpResponseMessage second = await client.PostAsync(
      new Uri($"reservation-spot-items/{item.Id}/return-keys", UriKind.Relative), content: null);

    second.StatusCode.ShouldBe(HttpStatusCode.NoContent);
  }
}
