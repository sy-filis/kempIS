using Application.Reservations.Queries.GetReservationForGuest;
using Domain.Reservations.ReservationStates;
using SharedKernel;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Web.Api.IntegrationTests.Reservations;

public sealed class GuestReservationEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;
  private readonly HttpClient _client;

  public GuestReservationEndpointTests(ApiFactory factory)
  {
    _factory = factory;
    _client = factory.CreateClient();
  }

  public Task InitializeAsync() => _factory.ResetReservationsAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  private async Task<DomainReservation> SeedReservation(string secret)
  {
    DomainReservation r = new ReservationBuilder()
      .InState(ReservationState.Created)
      .WithSecret(secret)
      .Build();
    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(r);
      await db.SaveChangesAsync();
    });
    return r;
  }

  [Fact]
  public async Task GetGuest_CorrectSecret_Returns200()
  {
    DomainReservation r = await SeedReservation("sekret-123");

    HttpResponseMessage response = await _client.GetAsync(
      new Uri($"reservations/{r.Id}/guest?secret=sekret-123", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    ReservationForGuestResponse? body = await response.Content.ReadFromJsonAsync<ReservationForGuestResponse>();
    body.ShouldNotBeNull();
    body.Id.ShouldBe(r.Id);
    body.State.ShouldBe("Created");
  }

  [Fact]
  public async Task GetGuest_WrongSecret_Returns400OrProblem()
  {
    DomainReservation r = await SeedReservation("real");

    HttpResponseMessage response = await _client.GetAsync(
      new Uri($"reservations/{r.Id}/guest?secret=wrong", UriKind.Relative));

    response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task GetGuest_MissingReservation_Returns404()
  {
    HttpResponseMessage response = await _client.GetAsync(
      new Uri($"reservations/{Guid.NewGuid()}/guest?secret=anything", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task PostGuestCancel_CorrectSecret_Returns204_AndCancelsReservation()
  {
    DomainReservation r = await SeedReservation("cancel-me");

    HttpResponseMessage response = await _client.PostAsync(
      new Uri($"reservations/{r.Id}/guest/cancel?secret=cancel-me", UriKind.Relative), content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    await _factory.WithDbAsync(async db =>
    {
      DomainReservation reloaded = await db.Reservations.AsNoTracking().SingleAsync(x => x.Id == r.Id);
      reloaded.State.ShouldBe(ReservationState.Cancelled);
    });
  }

  [Fact]
  public async Task PostGuestCancel_WrongSecret_DoesNotCancel()
  {
    DomainReservation r = await SeedReservation("real");

    HttpResponseMessage response = await _client.PostAsync(
      new Uri($"reservations/{r.Id}/guest/cancel?secret=wrong", UriKind.Relative), content: null);

    response.StatusCode.ShouldNotBe(HttpStatusCode.NoContent);
    await _factory.WithDbAsync(async db =>
    {
      DomainReservation reloaded = await db.Reservations.AsNoTracking().SingleAsync(x => x.Id == r.Id);
      reloaded.State.ShouldBe(ReservationState.Created);
    });
  }
}
