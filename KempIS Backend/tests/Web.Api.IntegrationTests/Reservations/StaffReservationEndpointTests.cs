using System.Text.Json;
using Application.Abstractions.Authentication;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.ReservationStates;
using SharedKernel;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Web.Api.IntegrationTests.Reservations;

public sealed class StaffReservationEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public StaffReservationEndpointTests(ApiFactory factory) => _factory = factory;

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
  public async Task PostReservation_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().PostAsJsonAsync(
      new Uri("reservations", UriKind.Relative), MinimalCreateRequest(Guid.NewGuid()));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task PostReservation_WrongRole_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.CleaningStaff).PostAsJsonAsync(
      new Uri("reservations", UriKind.Relative), MinimalCreateRequest(Guid.NewGuid()));

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task PostReservationCancel_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().PostAsync(
      new Uri($"reservations/{Guid.NewGuid()}/cancel", UriKind.Relative), content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task PostReservation_Receptionist_Returns201AndPersists()
  {
    var groupId = Guid.NewGuid();
    var spotId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.SpotGroups.Add(new SpotGroupBuilder().WithId(groupId).WithCapacity(5).Build());
      db.Spots.Add(new SpotBuilder().WithId(spotId).InGroup(groupId).Build());
      await db.SaveChangesAsync();
    });
    _factory.AvailabilityChecker.CheckAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(Result.Success());

    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("reservations", UriKind.Relative),
      MinimalCreateRequest(spotId));

    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    await _factory.WithDbAsync(async db =>
    {
      (await db.Reservations.CountAsync()).ShouldBe(1);
    });
  }

  [Fact]
  public async Task PostReservation_InvalidPayload_Manager_Returns400()
  {
    var request = new
    {
      Name = "",
      Surname = "",
      Email = "bad",
      Phone = "",
      From = default(DateOnly),
      To = default(DateOnly),
      SpotIds = Array.Empty<Guid>(),
      Note = (string?)null,
      GroupReservationId = (Guid?)null,
    };

    HttpResponseMessage response = await Client(Roles.Manager).PostAsJsonAsync(
      new Uri("reservations", UriKind.Relative), request);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task PostCancel_ExistingReservation_Manager_Returns204()
  {
    DomainReservation r = new ReservationBuilder().InState(ReservationState.Confirmed).Build();
    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(r);
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Manager).PostAsync(
      new Uri($"reservations/{r.Id}/cancel", UriKind.Relative), content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    await _factory.WithDbAsync(async db =>
    {
      DomainReservation reloaded = await db.Reservations.AsNoTracking().SingleAsync(x => x.Id == r.Id);
      reloaded.State.ShouldBe(ReservationState.Cancelled);
    });
  }

  [Fact]
  public async Task PostCancel_Missing_Manager_Returns404()
  {
    HttpResponseMessage response = await Client(Roles.Manager).PostAsync(
      new Uri($"reservations/{Guid.NewGuid()}/cancel", UriKind.Relative), content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task PutReservation_Manager_Returns204AndPersists()
  {
    var groupId = Guid.NewGuid();
    var spotId = Guid.NewGuid();
    DomainReservation r = new ReservationBuilder().InState(ReservationState.Confirmed)
      .For(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3)).Build();
    await _factory.WithDbAsync(async db =>
    {
      db.SpotGroups.Add(new SpotGroupBuilder().WithId(groupId).WithCapacity(5).Build());
      db.Spots.Add(new SpotBuilder().WithId(spotId).InGroup(groupId).Build());
      db.Reservations.Add(r);
      await db.SaveChangesAsync();
    });
    _factory.AvailabilityChecker.CheckAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(Result.Success());

    var request = new
    {
      Name = "Jan",
      Surname = "Novak",
      Email = "jan@example.com",
      Phone = "+420",
      From = new DateOnly(2026, 6, 2),
      To = new DateOnly(2026, 6, 5),
      Note = "updated",
      GroupReservationId = (Guid?)null,
      SpotIds = new[] { spotId },
      Services = Array.Empty<object>(),
      Vehicles = Array.Empty<object>(),
    };

    HttpResponseMessage response = await Client(Roles.Manager).PutAsJsonAsync(
      new Uri($"reservations/{r.Id}", UriKind.Relative), request);

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
    await _factory.WithDbAsync(async db =>
    {
      DomainReservation reloaded = await db.Reservations.AsNoTracking().SingleAsync(x => x.Id == r.Id);
      reloaded.Period.From.ShouldBe(new DateOnly(2026, 6, 2));
      reloaded.Period.To.ShouldBe(new DateOnly(2026, 6, 5));
      reloaded.Note.ShouldBe("updated");
    });
  }

  [Fact]
  public async Task PutReservation_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().PutAsJsonAsync(
      new Uri($"reservations/{Guid.NewGuid()}", UriKind.Relative), new
      {
        Name = "Jan",
        Surname = "Novak",
        Email = "jan@example.com",
        Phone = "+420",
        From = new DateOnly(2026, 6, 1),
        To = new DateOnly(2026, 6, 2),
        Note = (string?)null,
        GroupReservationId = (Guid?)null,
        SpotIds = Array.Empty<Guid>(),
        Services = Array.Empty<object>(),
        Vehicles = Array.Empty<object>(),
      });

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GetReservations_Receptionist_ReturnsFilteredList()
  {
    DomainReservation inRange = new ReservationBuilder()
      .For(new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 12))
      .InState(ReservationState.Confirmed).Build();
    DomainReservation outOfRange = new ReservationBuilder()
      .For(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 3))
      .InState(ReservationState.Confirmed).Build();
    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.AddRange(inRange, outOfRange);
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("reservations?from=2026-06-01&to=2026-06-30", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    string body = await response.Content.ReadAsStringAsync();
    body.ShouldContain(inRange.Id.ToString());
    body.ShouldNotContain(outOfRange.Id.ToString());
  }

  [Fact]
  public async Task GetReservations_StatusOnlyNoPeriod_ReturnsAllInThatStateAcrossYears()
  {
    DomainReservation createdInJune2025 = new ReservationBuilder()
      .WithId(Guid.NewGuid())
      .For(new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 5))
      .InState(ReservationState.Created).Build();
    DomainReservation createdInJanuary2027 = new ReservationBuilder()
      .WithId(Guid.NewGuid())
      .For(new DateOnly(2027, 1, 10), new DateOnly(2027, 1, 12))
      .InState(ReservationState.Created).Build();
    DomainReservation confirmedInJune2026 = new ReservationBuilder()
      .WithId(Guid.NewGuid())
      .For(new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 12))
      .InState(ReservationState.Confirmed).Build();
    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.AddRange(createdInJune2025, createdInJanuary2027, confirmedInJune2026);
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("reservations?status=Created", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    string body = await response.Content.ReadAsStringAsync();
    body.ShouldContain(createdInJune2025.Id.ToString());
    body.ShouldContain(createdInJanuary2027.Id.ToString());
    body.ShouldNotContain(confirmedInJune2026.Id.ToString());
  }

  [Fact]
  public async Task GetReservations_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(
      new Uri("reservations?from=2026-06-01&to=2026-06-30", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GetReservations_WrongRole_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.CleaningStaff).GetAsync(
      new Uri("reservations?from=2026-06-01&to=2026-06-30", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task PostCheckIn_ConfirmedReservation_Receptionist_Returns204()
  {
    DomainReservation r = new ReservationBuilder().InState(ReservationState.Confirmed).Build();
    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(r);
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsync(
      new Uri($"reservations/{r.Id}/check-in", UriKind.Relative), content: null);

    response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    await _factory.WithDbAsync(async db =>
    {
      (await db.Reservations.AsNoTracking().SingleAsync(x => x.Id == r.Id))
        .State.ShouldBe(ReservationState.CheckedIn);
    });
  }

  [Fact]
  public async Task PostThenPutReservation_Manager_Roundtrip_Succeeds()
  {
    var groupId = Guid.NewGuid();
    var spotIdA = Guid.NewGuid();
    var spotIdB = Guid.NewGuid();
    var serviceTypeId = Guid.NewGuid();
    var vatId = Guid.NewGuid();
    var serviceIdA = Guid.NewGuid();
    var serviceIdB = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.SpotGroups.Add(new SpotGroupBuilder().WithId(groupId).WithCapacity(5).Build());
      db.Spots.Add(new SpotBuilder().WithId(spotIdA).InGroup(groupId).WithName("A").Build());
      db.Spots.Add(new SpotBuilder().WithId(spotIdB).InGroup(groupId).WithName("B").Build());
      db.ServiceTypes.Add(new Domain.Services.ServiceTypes.ServiceType
      {
        Id = serviceTypeId,
        Name = "T",
        IsActive = true,
      });
      db.VatRates.Add(new Domain.Services.VatRates.VatRate
      {
        Id = vatId,
        Name = "Zero",
        Rate = 0m,
        IsActive = true,
      });
      db.Services.Add(new Domain.Services.Services.Service
      {
        Id = serviceIdA,
        Name = "A",
        ServiceGroup = Domain.Services.Services.ServiceGroup.Others,
        ServiceTypeId = serviceTypeId,
        VatRateId = vatId,
        BasePrice = 0m,
        IsActive = true,
      });
      db.Services.Add(new Domain.Services.Services.Service
      {
        Id = serviceIdB,
        Name = "B",
        ServiceGroup = Domain.Services.Services.ServiceGroup.Others,
        ServiceTypeId = serviceTypeId,
        VatRateId = vatId,
        BasePrice = 0m,
        IsActive = true,
      });
      await db.SaveChangesAsync();
    });
    _factory.AvailabilityChecker.CheckAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(Result.Success());

    HttpClient client = Client(Roles.Manager);

    var createBody = new
    {
      Name = "Anna",
      Surname = "Smith",
      Email = "anna@example.com",
      Phone = "+420111222333",
      From = new DateOnly(2026, 6, 1),
      To = new DateOnly(2026, 6, 5),
      Note = (string?)null,
      GroupReservationId = (Guid?)null,
      SpotIds = new[] { spotIdA },
      Services = new[]
      {
        new { ServiceId = serviceIdA, Quantity = 2u, RecapSingleQuantity = 0u, RecapDayQuantity = 0u },
      },
      Vehicles = new[] { new { Id = (Guid?)null, RegistrationNumber = "ABC-1234" } },
    };

    HttpResponseMessage post = await client.PostAsJsonAsync(
      new Uri("reservations", UriKind.Relative), createBody);
    post.StatusCode.ShouldBe(HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
    Guid id = await post.Content.ReadFromJsonAsync<Guid>();

    Guid existingVehicleId = Guid.Empty;
    await _factory.WithDbAsync(async db =>
    {
      existingVehicleId = await db.Vehicles
        .Where(v => v.ReservationId == id)
        .Select(v => v.Id)
        .SingleAsync();
    });

    var putBody = new
    {
      Name = "Eva",
      Surname = "Bila",
      Email = "eva@example.com",
      Phone = "+420555444333",
      From = new DateOnly(2026, 6, 2),
      To = new DateOnly(2026, 6, 6),
      Note = "rescheduled",
      GroupReservationId = (Guid?)null,
      SpotIds = new[] { spotIdB },
      Services = new[]
      {
        new { ServiceId = serviceIdB, Quantity = 5u, RecapSingleQuantity = 1u, RecapDayQuantity = 2u },
      },
      Vehicles = new[]
      {
        new { Id = (Guid?)existingVehicleId, RegistrationNumber = "ABC-9999" },
        new { Id = (Guid?)null, RegistrationNumber = "DEF-0000" },
      },
    };

    HttpResponseMessage put = await client.PutAsJsonAsync(
      new Uri($"reservations/{id}", UriKind.Relative), putBody);
    put.StatusCode.ShouldBe(HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? putEx) ? putEx.ToString() : "no exception");

    await _factory.WithDbAsync(async db =>
    {
      DomainReservation reloaded = await db.Reservations.AsNoTracking()
        .SingleAsync(r => r.Id == id);
      reloaded.ReservationMaker.Name.ShouldBe("Eva");
      reloaded.Note.ShouldBe("rescheduled");
      reloaded.Period.From.ShouldBe(new DateOnly(2026, 6, 2));

      Guid[] spotIds = await db.ReservationSpotItems
        .Where(s => s.ReservationId == id)
        .Select(s => s.SpotId!.Value)
        .ToArrayAsync();
      spotIds.ShouldBe([spotIdB]);

      Guid[] serviceIds = await db.ReservationServiceItems
        .Where(s => s.ReservationId == id)
        .Select(s => s.ServiceId)
        .ToArrayAsync();
      serviceIds.ShouldBe([serviceIdB]);

      string[] plates = await db.Vehicles
        .Where(v => v.ReservationId == id)
        .OrderBy(v => v.RegistrationNumber)
        .Select(v => v.RegistrationNumber)
        .ToArrayAsync();
      plates.ShouldBe(["ABC-9999", "DEF-0000"]);
    });
  }

  [Fact]
  public async Task PutReservation_Cancelled_Manager_Returns409()
  {
    DomainReservation r = new ReservationBuilder().InState(ReservationState.Cancelled)
      .For(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3)).Build();
    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(r);
      await db.SaveChangesAsync();
    });

    var request = new
    {
      Name = "Jan",
      Surname = "Novak",
      Email = "jan@example.com",
      Phone = "+420",
      From = new DateOnly(2026, 6, 1),
      To = new DateOnly(2026, 6, 3),
      Note = (string?)null,
      GroupReservationId = (Guid?)null,
      SpotIds = new[] { Guid.NewGuid() },
      Services = Array.Empty<object>(),
      Vehicles = Array.Empty<object>(),
    };

    HttpResponseMessage response = await Client(Roles.Manager).PutAsJsonAsync(
      new Uri($"reservations/{r.Id}", UriKind.Relative), request);

    response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
  }

  [Fact]
  public async Task PostReservation_DuplicateServiceIds_Manager_Returns400()
  {
    var groupId = Guid.NewGuid();
    var spotId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.SpotGroups.Add(new SpotGroupBuilder().WithId(groupId).WithCapacity(5).Build());
      db.Spots.Add(new SpotBuilder().WithId(spotId).InGroup(groupId).Build());
      await db.SaveChangesAsync();
    });

    var body = new
    {
      Name = "Jan",
      Surname = "Novak",
      Email = "jan@example.com",
      Phone = "+420",
      From = new DateOnly(2026, 6, 1),
      To = new DateOnly(2026, 6, 3),
      Note = (string?)null,
      GroupReservationId = (Guid?)null,
      SpotIds = new[] { spotId },
      Services = new[]
      {
        new { ServiceId = serviceId, Quantity = 1u, RecapSingleQuantity = 0u, RecapDayQuantity = 0u },
        new { ServiceId = serviceId, Quantity = 2u, RecapSingleQuantity = 0u, RecapDayQuantity = 0u },
      },
      Vehicles = Array.Empty<object>(),
    };

    HttpResponseMessage response = await Client(Roles.Manager).PostAsJsonAsync(
      new Uri("reservations", UriKind.Relative), body);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Reservation_DisplayName_RoundTripsThroughCreateUpdateAndGet()
  {
    var groupId = Guid.NewGuid();
    var spotId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.SpotGroups.Add(new SpotGroupBuilder().WithId(groupId).WithCapacity(5).Build());
      db.Spots.Add(new SpotBuilder().WithId(spotId).InGroup(groupId).Build());
      await db.SaveChangesAsync();
    });
    _factory.AvailabilityChecker.CheckAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(Result.Success());

    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage createResponse = await client.PostAsJsonAsync(
      new Uri("reservations", UriKind.Relative),
      new
      {
        Name = "Jan",
        Surname = "Novak",
        Email = "jan@example.com",
        Phone = "+420000000000",
        From = new DateOnly(2026, 9, 1),
        To = new DateOnly(2026, 9, 3),
        SpotIds = new[] { spotId },
        Note = (string?)null,
        DisplayName = "Smith family",
        Services = Array.Empty<object>(),
        Vehicles = Array.Empty<object>(),
      });
    createResponse.StatusCode.ShouldBe(HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
    Guid createdId = await createResponse.Content.ReadFromJsonAsync<Guid>();

    HttpResponseMessage getAfterCreate = await client.GetAsync(
      new Uri($"reservations/{createdId}", UriKind.Relative));
    getAfterCreate.StatusCode.ShouldBe(HttpStatusCode.OK);
    using var afterCreate = JsonDocument.Parse(await getAfterCreate.Content.ReadAsStringAsync());
    afterCreate.RootElement.GetProperty("displayName").GetString().ShouldBe("Smith family");

    HttpResponseMessage putResponse = await client.PutAsJsonAsync(
      new Uri($"reservations/{createdId}", UriKind.Relative),
      new
      {
        Name = "Jan",
        Surname = "Novak",
        Email = "jan@example.com",
        Phone = "+420000000000",
        From = new DateOnly(2026, 9, 1),
        To = new DateOnly(2026, 9, 3),
        Note = (string?)null,
        GroupReservationId = (Guid?)null,
        SpotIds = new[] { spotId },
        DisplayName = "Smith family updated",
        Services = Array.Empty<object>(),
        Vehicles = Array.Empty<object>(),
      });
    putResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex2) ? ex2.ToString() : "no exception");

    HttpResponseMessage getAfterPut = await client.GetAsync(
      new Uri($"reservations/{createdId}", UriKind.Relative));
    getAfterPut.StatusCode.ShouldBe(HttpStatusCode.OK);
    using var afterPut = JsonDocument.Parse(await getAfterPut.Content.ReadAsStringAsync());
    afterPut.RootElement.GetProperty("displayName").GetString().ShouldBe("Smith family updated");

    HttpResponseMessage putClear = await client.PutAsJsonAsync(
      new Uri($"reservations/{createdId}", UriKind.Relative),
      new
      {
        Name = "Jan",
        Surname = "Novak",
        Email = "jan@example.com",
        Phone = "+420000000000",
        From = new DateOnly(2026, 9, 1),
        To = new DateOnly(2026, 9, 3),
        Note = (string?)null,
        GroupReservationId = (Guid?)null,
        SpotIds = new[] { spotId },
        DisplayName = (string?)null,
        Services = Array.Empty<object>(),
        Vehicles = Array.Empty<object>(),
      });
    putClear.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    HttpResponseMessage getAfterClear = await client.GetAsync(
      new Uri($"reservations/{createdId}", UriKind.Relative));
    getAfterClear.StatusCode.ShouldBe(HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex3) ? ex3.ToString() : "no exception");
    using var afterClear = JsonDocument.Parse(await getAfterClear.Content.ReadAsStringAsync());
    afterClear.RootElement.GetProperty("displayName").ValueKind.ShouldBe(JsonValueKind.Null);
  }

  private static object MinimalCreateRequest(Guid spotId) => new
  {
    Name = "Jan",
    Surname = "Novak",
    Email = "jan@example.com",
    Phone = "+420",
    From = new DateOnly(2026, 6, 1),
    To = new DateOnly(2026, 6, 3),
    Note = (string?)null,
    GroupReservationId = (Guid?)null,
    SpotIds = new[] { spotId },
    Services = Array.Empty<object>(),
    Vehicles = Array.Empty<object>(),
  };
}
