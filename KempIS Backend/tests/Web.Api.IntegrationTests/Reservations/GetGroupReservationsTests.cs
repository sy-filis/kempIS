using Application.Abstractions.Authentication;
using Domain.Reservations.GroupReservations;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reservations;

public sealed class GetGroupReservationsTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public GetGroupReservationsTests(ApiFactory factory) => _factory = factory;

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
  public async Task Get_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(
      new Uri("group-reservations?from=2026-07-10&to=2026-07-20", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Get_WrongRole_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.CleaningStaff).GetAsync(
      new Uri("group-reservations?from=2026-07-10&to=2026-07-20", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Get_OverlappingRow_Returns200WithItem()
  {
    var s1 = Guid.NewGuid();
    var s2 = Guid.NewGuid();
    GroupReservation gr = new GroupReservationBuilder()
      .For(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14))
      .WithOrganizer("Alice", "alice@example.com")
      .WithOrganizerPhone("+420 999 111 222")
      .HoldingSpots(s1, s2)
      .Build();
    await _factory.WithDbAsync(async db =>
    {
      db.GroupReservations.Add(gr);
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("group-reservations?from=2026-07-10&to=2026-07-20", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    List<GroupReservationListItemDto> items =
      (await response.Content.ReadFromJsonAsync<List<GroupReservationListItemDto>>())!;
    items.Count.ShouldBe(1);
    items[0].Id.ShouldBe(gr.Id);
    items[0].State.ShouldBe("Confirmed");
    items[0].OrganizerPhone.ShouldBe("+420 999 111 222");
    items[0].SpotIds.ShouldBe(new[] { s1, s2 }, ignoreOrder: true);
  }

  [Fact]
  public async Task Get_RowOutsideRange_Returns200WithEmptyList()
  {
    await _factory.WithDbAsync(async db =>
    {
      db.GroupReservations.Add(new GroupReservationBuilder()
        .For(new DateOnly(2026, 7, 25), new DateOnly(2026, 7, 30))
        .Build());
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("group-reservations?from=2026-07-10&to=2026-07-20", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await response.Content.ReadFromJsonAsync<List<GroupReservationListItemDto>>())!
      .ShouldBeEmpty();
  }

  [Fact]
  public async Task Get_StateFilter_ReturnsOnlyMatching()
  {
    var createdId = Guid.NewGuid();
    var cancelledId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.GroupReservations.Add(new GroupReservationBuilder()
        .WithId(createdId)
        .InState(GroupReservationState.Confirmed)
        .For(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14))
        .Build());
      db.GroupReservations.Add(new GroupReservationBuilder()
        .WithId(cancelledId)
        .InState(GroupReservationState.Canceled)
        .For(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14))
        .Build());
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("group-reservations?from=2026-07-10&to=2026-07-20&state=Canceled", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    List<GroupReservationListItemDto> items =
      (await response.Content.ReadFromJsonAsync<List<GroupReservationListItemDto>>())!;
    items.ShouldHaveSingleItem().Id.ShouldBe(cancelledId);
  }

  [Fact]
  public async Task Get_InvalidStateValue_Returns400()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("group-reservations?from=2026-07-10&to=2026-07-20&state=Bogus", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  private sealed record GroupReservationListItemDto(
    Guid Id,
    string State,
    DateOnly From,
    DateOnly To,
    string OrganizerName,
    string OrganizerEmail,
    string OrganizerPhone,
    IReadOnlyList<Guid> SpotIds,
    DateTime CreatedAtUtc);
}
