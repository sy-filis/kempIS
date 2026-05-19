using System.Text.Json;
using Application.Abstractions.Authentication;
using Domain.Common;
using Domain.Reservations.GroupReservations;
using SharedKernel;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reservations;

public sealed class GroupReservationEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public GroupReservationEndpointTests(ApiFactory factory) => _factory = factory;

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
  public async Task PostGroupReservation_Receptionist_Returns201AndPersists()
  {
    var spotGroupId = Guid.NewGuid();
    var spotId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.SpotGroups.Add(new SpotGroupBuilder().WithId(spotGroupId).WithCapacity(3).Build());
      db.Spots.Add(new SpotBuilder().WithId(spotId).InGroup(spotGroupId).Build());
      await db.SaveChangesAsync();
    });
    _factory.AvailabilityChecker.CheckAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(Result.Success());

    var request = new
    {
      From = new DateOnly(2026, 7, 1),
      To = new DateOnly(2026, 7, 5),
      SpotIds = new[] { spotId },
      OrganizerName = "Alice",
      OrganizerEmail = "alice@example.com",
      OrganizerPhone = "+420 777 111 222",
      Note = (string?)null,
      Language = "cs",
    };

    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("group-reservations", UriKind.Relative), request);

    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    await _factory.WithDbAsync(async db =>
    {
      GroupReservation persisted = await db.GroupReservations.AsNoTracking().SingleAsync();
      persisted.OrganizerPhone.ShouldBe("+420 777 111 222");
    });
  }

  [Fact]
  public async Task PostGroupReservation_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().PostAsJsonAsync(
      new Uri("group-reservations", UriKind.Relative), new
      {
        From = new DateOnly(2026, 7, 1),
        To = new DateOnly(2026, 7, 2),
        SpotIds = new[] { Guid.NewGuid() },
        OrganizerName = "A",
        OrganizerEmail = "a@b.c",
        Note = (string?)null,
      });

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task PostGroupReservation_WrongRole_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.CleaningStaff).PostAsJsonAsync(
      new Uri("group-reservations", UriKind.Relative), new
      {
        From = new DateOnly(2026, 7, 1),
        To = new DateOnly(2026, 7, 2),
        SpotIds = new[] { Guid.NewGuid() },
        OrganizerName = "A",
        OrganizerEmail = "a@b.c",
        Note = (string?)null,
      });

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task GetGroupReservation_Existing_Manager_Returns200()
  {
    GroupReservation gr = new GroupReservationBuilder().Build();
    await _factory.WithDbAsync(async db =>
    {
      db.GroupReservations.Add(gr);
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Manager).GetAsync(
      new Uri($"group-reservations/{gr.Id}", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    string body = await response.Content.ReadAsStringAsync();
    body.ShouldContain(gr.Id.ToString());
  }

  [Fact]
  public async Task GetGroupReservation_Missing_Manager_Returns404()
  {
    HttpResponseMessage response = await Client(Roles.Manager).GetAsync(
      new Uri($"group-reservations/{Guid.NewGuid()}", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task PostCancelGroupReservation_Manager_Returns204()
  {
    GroupReservation gr = new GroupReservationBuilder().InState(GroupReservationState.Confirmed).Build();
    await _factory.WithDbAsync(async db =>
    {
      db.GroupReservations.Add(gr);
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Manager).PostAsync(
      new Uri($"group-reservations/{gr.Id}/cancel", UriKind.Relative), content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    await _factory.WithDbAsync(async db =>
    {
      GroupReservation reloaded = await db.GroupReservations.AsNoTracking().SingleAsync(x => x.Id == gr.Id);
      reloaded.State.ShouldBe(GroupReservationState.Canceled);
    });
  }

  [Fact]
  public async Task PostSendInvitation_Manager_Returns204AndSendsEmail()
  {
    GroupReservation gr = new GroupReservationBuilder().InState(GroupReservationState.Confirmed).Build();
    await _factory.WithDbAsync(async db =>
    {
      db.GroupReservations.Add(gr);
      await db.SaveChangesAsync();
    });

    int before = _factory.EmailSender.Sent.Count;
    HttpResponseMessage response = await Client(Roles.Manager).PostAsJsonAsync(
      new Uri($"group-reservations/{gr.Id}/send-invitation", UriKind.Relative),
      new { Language = "en" });

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    _factory.EmailSender.Sent.Count.ShouldBeGreaterThan(before);
  }

  private async Task<(Guid groupId, Guid spotId, Guid newSpotId)> SeedGroupForUpdateAsync(
    GroupReservationState state = GroupReservationState.Confirmed)
  {
    var spotGroupId = Guid.NewGuid();
    var spotId = Guid.NewGuid();
    var newSpotId = Guid.NewGuid();
    var groupId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.SpotGroups.Add(new SpotGroupBuilder().WithId(spotGroupId).WithCapacity(3).Build());
      db.Spots.Add(new SpotBuilder().WithId(spotId).InGroup(spotGroupId).Build());
      db.Spots.Add(new SpotBuilder().WithId(newSpotId).InGroup(spotGroupId).Build());
      db.GroupReservations.Add(new GroupReservation
      {
        Id = groupId,
        Number = $"GR-TEST/SEED-{Guid.NewGuid():N}",
        State = state,
        Period = new DateRange(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 5)),
        Secret = "seed-secret",
        OrganizerName = "Original",
        OrganizerEmail = "original@example.com",
        OrganizerPhone = "+420 700 000 000",
        Note = null,
        CreatedAtUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        HeldSpots = [new GroupReservationSpot { SpotId = spotId }],
      });
      await db.SaveChangesAsync();
    });
    return (groupId, spotId, newSpotId);
  }

  private static object UpdateBody(Guid spotId, string organizerName = "Updated") => new
  {
    From = new DateOnly(2026, 8, 1),
    To = new DateOnly(2026, 8, 10),
    SpotIds = new[] { spotId },
    OrganizerName = organizerName,
    OrganizerEmail = "updated@example.com",
    OrganizerPhone = "+420 777 999 888",
    Note = (string?)"Updated note",
  };

  [Fact]
  public async Task PutGroupReservation_Receptionist_Returns204AndPersists()
  {
    (Guid groupId, _, Guid newSpotId) = await SeedGroupForUpdateAsync();

    HttpResponseMessage response = await Client(Roles.Receptionist).PutAsJsonAsync(
      new Uri($"group-reservations/{groupId}", UriKind.Relative), UpdateBody(newSpotId));

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
    await _factory.WithDbAsync(async db =>
    {
      GroupReservation persisted = await db.GroupReservations
        .Include(g => g.HeldSpots)
        .AsNoTracking()
        .SingleAsync(g => g.Id == groupId);
      persisted.OrganizerName.ShouldBe("Updated");
      persisted.OrganizerEmail.ShouldBe("updated@example.com");
      persisted.Period.From.ShouldBe(new DateOnly(2026, 8, 1));
      persisted.Period.To.ShouldBe(new DateOnly(2026, 8, 10));
      persisted.HeldSpots.Single().SpotId.ShouldBe(newSpotId);
      persisted.UpdatedAtUtc.ShouldNotBeNull();
    });
  }

  [Fact]
  public async Task PutGroupReservation_UnknownId_Returns404()
  {
    HttpResponseMessage response = await Client(Roles.Manager).PutAsJsonAsync(
      new Uri($"group-reservations/{Guid.NewGuid()}", UriKind.Relative), UpdateBody(Guid.NewGuid()));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task PutGroupReservation_Canceled_Returns400()
  {
    (Guid groupId, _, Guid newSpotId) = await SeedGroupForUpdateAsync(state: GroupReservationState.Canceled);

    HttpResponseMessage response = await Client(Roles.Manager).PutAsJsonAsync(
      new Uri($"group-reservations/{groupId}", UriKind.Relative), UpdateBody(newSpotId));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task PutGroupReservation_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().PutAsJsonAsync(
      new Uri($"group-reservations/{Guid.NewGuid()}", UriKind.Relative), UpdateBody(Guid.NewGuid()));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task PutGroupReservation_GuestRole_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.Guest).PutAsJsonAsync(
      new Uri($"group-reservations/{Guid.NewGuid()}", UriKind.Relative), UpdateBody(Guid.NewGuid()));

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task GroupReservation_DisplayName_RoundTripsThroughCreateUpdateAndGet()
  {
    var spotGroupId = Guid.NewGuid();
    var spotId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.SpotGroups.Add(new SpotGroupBuilder().WithId(spotGroupId).WithCapacity(3).Build());
      db.Spots.Add(new SpotBuilder().WithId(spotId).InGroup(spotGroupId).Build());
      await db.SaveChangesAsync();
    });
    _factory.AvailabilityChecker.CheckAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(Result.Success());

    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage createResponse = await client.PostAsJsonAsync(
      new Uri("group-reservations", UriKind.Relative),
      new
      {
        From = new DateOnly(2026, 9, 1),
        To = new DateOnly(2026, 9, 5),
        SpotIds = new[] { spotId },
        OrganizerName = "Alice",
        OrganizerEmail = "alice@example.com",
        OrganizerPhone = "+420 777 111 222",
        Note = (string?)null,
        Language = "cs",
        DisplayName = "Company retreat",
      });
    createResponse.StatusCode.ShouldBe(HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
    using var createdBody = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
    Guid createdId = createdBody.RootElement.GetProperty("id").GetGuid();

    HttpResponseMessage getAfterCreate = await client.GetAsync(
      new Uri($"group-reservations/{createdId}", UriKind.Relative));
    getAfterCreate.StatusCode.ShouldBe(HttpStatusCode.OK);
    using var afterCreate = JsonDocument.Parse(await getAfterCreate.Content.ReadAsStringAsync());
    afterCreate.RootElement.GetProperty("displayName").GetString().ShouldBe("Company retreat");

    HttpResponseMessage putResponse = await client.PutAsJsonAsync(
      new Uri($"group-reservations/{createdId}", UriKind.Relative),
      new
      {
        From = new DateOnly(2026, 9, 1),
        To = new DateOnly(2026, 9, 5),
        SpotIds = new[] { spotId },
        OrganizerName = "Alice",
        OrganizerEmail = "alice@example.com",
        OrganizerPhone = "+420 777 111 222",
        Note = (string?)null,
        DisplayName = "Company retreat 2026",
      });
    putResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex2) ? ex2.ToString() : "no exception");

    HttpResponseMessage getAfterPut = await client.GetAsync(
      new Uri($"group-reservations/{createdId}", UriKind.Relative));
    getAfterPut.StatusCode.ShouldBe(HttpStatusCode.OK);
    using var afterPut = JsonDocument.Parse(await getAfterPut.Content.ReadAsStringAsync());
    afterPut.RootElement.GetProperty("displayName").GetString().ShouldBe("Company retreat 2026");

    HttpResponseMessage putClear = await client.PutAsJsonAsync(
      new Uri($"group-reservations/{createdId}", UriKind.Relative),
      new
      {
        From = new DateOnly(2026, 9, 1),
        To = new DateOnly(2026, 9, 5),
        SpotIds = new[] { spotId },
        OrganizerName = "Alice",
        OrganizerEmail = "alice@example.com",
        OrganizerPhone = "+420 777 111 222",
        Note = (string?)null,
        DisplayName = (string?)null,
      });
    putClear.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    HttpResponseMessage getAfterClear = await client.GetAsync(
      new Uri($"group-reservations/{createdId}", UriKind.Relative));
    getAfterClear.StatusCode.ShouldBe(HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex3) ? ex3.ToString() : "no exception");
    using var afterClear = JsonDocument.Parse(await getAfterClear.Content.ReadAsStringAsync());
    afterClear.RootElement.GetProperty("displayName").ValueKind.ShouldBe(JsonValueKind.Null);
  }
}
