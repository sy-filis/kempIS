using Application.Abstractions.Authentication;
using Domain.Common;
using Domain.Reservations.Guests;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reservations;

public sealed class NationalitiesEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public NationalitiesEndpointTests(ApiFactory factory) => _factory = factory;

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
  public async Task GetNationalities_ReturnsSeededCountries()
  {
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage response = await client.GetAsync(new Uri("nationalities", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
    string body = await response.Content.ReadAsStringAsync();
    body.ShouldContain("\"alpha3\":\"CZE\"");
    body.ShouldContain("\"alpha3\":\"DEU\"");
    body.ShouldContain("Česko");
    body.ShouldContain("\"nameEn\":\"Czechia\"");
    body.ShouldContain("\"nameEn\":\"Germany\"");
  }

  [Fact]
  public async Task GetNationalities_CzechCountry_HasCzechLanguageCode()
  {
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage response = await client.GetAsync(new Uri("nationalities", UriKind.Relative));
    string body = await response.Content.ReadAsStringAsync();

    body.ShouldContain("\"alpha3\":\"CZE\"");
    body.ShouldContain("\"languageCode\":\"cs\"");
    body.ShouldContain("\"alpha3\":\"DEU\"");
    body.ShouldContain("\"languageCode\":\"en\"");
  }

  [Fact]
  public async Task GetNationalities_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(new Uri("nationalities", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  private static object Request(
    Guid languageId,
    string alpha2 = "XX",
    string alpha3 = "XXX",
    string numeric = "999") => new
    {
      Name = "Testland",
      NameEn = "Testland EN",
      Alpha2 = alpha2,
      Alpha3 = alpha3,
      Numeric = numeric,
      VisaRequired = false,
      BiometricsRequired = false,
      IsEu = false,
      LanguageId = languageId,
    };

  private async Task<Guid> GetSeededLanguageIdAsync()
  {
    Guid id = Guid.Empty;
    await _factory.WithDbAsync(async db =>
    {
      Domain.Services.Languages.Language lang = await db.Languages.FirstAsync(l => l.Code == "en");
      id = lang.Id;
    });
    return id;
  }

  [Fact]
  public async Task Crud_Manager_Roundtrip_Succeeds()
  {
    HttpClient client = Client(Roles.Manager);
    Guid languageId = await GetSeededLanguageIdAsync();

    HttpResponseMessage postResponse = await client.PostAsJsonAsync(
      new Uri("nationalities", UriKind.Relative), Request(languageId));
    postResponse.StatusCode.ShouldBe(HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
    Guid id = await postResponse.Content.ReadFromJsonAsync<Guid>();

    HttpResponseMessage putResponse = await client.PutAsJsonAsync(
      new Uri($"nationalities/{id}", UriKind.Relative),
      Request(languageId, alpha2: "XA", alpha3: "XAA", numeric: "998"));
    putResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    HttpResponseMessage deleteResponse = await client.DeleteAsync(
      new Uri($"nationalities/{id}", UriKind.Relative));
    deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
  }

  [Fact]
  public async Task PostNationality_Receptionist_Returns403()
  {
    Guid languageId = await GetSeededLanguageIdAsync();

    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("nationalities", UriKind.Relative), Request(languageId));

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task PostNationality_DuplicateAlpha3_Returns409()
  {
    HttpClient client = Client(Roles.Manager);
    Guid languageId = await GetSeededLanguageIdAsync();

    HttpResponseMessage response = await client.PostAsJsonAsync(
      new Uri("nationalities", UriKind.Relative),
      Request(languageId, alpha2: "XX", alpha3: "CZE", numeric: "999"));

    response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
  }

  [Fact]
  public async Task PostNationality_UnknownLanguageId_Returns404()
  {
    HttpClient client = Client(Roles.Manager);

    HttpResponseMessage response = await client.PostAsJsonAsync(
      new Uri("nationalities", UriKind.Relative), Request(Guid.NewGuid()));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task PutNationality_UnknownId_Returns404()
  {
    HttpClient client = Client(Roles.Manager);
    Guid languageId = await GetSeededLanguageIdAsync();

    HttpResponseMessage response = await client.PutAsJsonAsync(
      new Uri($"nationalities/{Guid.NewGuid()}", UriKind.Relative), Request(languageId));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task DeleteNationality_ReferencedByGuest_Returns409()
  {
    HttpClient client = Client(Roles.Manager);

    Guid nationalityId = Guid.Empty;
    await _factory.WithDbAsync(async db =>
    {
      Domain.Reservations.Nationalities.Nationality cze = await db.Nationalities.FirstAsync(n => n.Alpha3 == "CZE");
      nationalityId = cze.Id;

      db.Guests.Add(new Domain.Reservations.Guests.Guest
      {
        Id = Guid.NewGuid(),
        ReservationId = Guid.NewGuid(),
        BillId = null,
        FirstName = "Jan",
        LastName = "Novak",
        NationalityId = nationalityId,
        DateOfBirth = new DateOnly(1990, 1, 1),
        DocumentType = DocumentType.Passport,
        DocumentNumber = "AB123456",
        Address = new Address(Guid.NewGuid(), "Brno", "60200", "Kolejni", "2"),
        ReasonOfStay = "Holiday",
        StayDateRange = new DateRange(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5)),
        VisaNumber = null,
        Note = null,
        Scartation = null,
        CheckInAt = null,
        CheckOutAt = null,
      });
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await client.DeleteAsync(
      new Uri($"nationalities/{nationalityId}", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
  }
}

public sealed class VehiclesEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public VehiclesEndpointTests(ApiFactory factory) => _factory = factory;

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

  private static readonly DateOnly ReservationFrom = new(2026, 6, 1);
  private static readonly DateOnly ReservationTo = new(2026, 6, 5);

  private async Task<Guid> SeedReservationAsync()
  {
    var reservationId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(new ReservationBuilder()
        .WithId(reservationId)
        .For(ReservationFrom, ReservationTo)
        .Build());
      await db.SaveChangesAsync();
    });
    return reservationId;
  }

  private static object Request(Guid reservationId, string plate = "1AB2345") => new
  {
    ReservationId = reservationId,
    BillId = Guid.NewGuid(),
    ServiceId = Guid.NewGuid(),
    RegistrationNumber = plate,
  };

  [Fact]
  public async Task Crud_Manager_Roundtrip_Succeeds()
  {
    HttpClient client = Client(Roles.Manager);
    Guid reservationId = await SeedReservationAsync();

    HttpResponseMessage postResponse = await client.PostAsJsonAsync(
      new Uri("vehicles", UriKind.Relative), Request(reservationId));
    postResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    Guid id = await postResponse.Content.ReadFromJsonAsync<Guid>();

    HttpResponseMessage putResponse = await client.PutAsJsonAsync(
      new Uri($"vehicles/{id}", UriKind.Relative), Request(reservationId, plate: "9XY8765"));
    putResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    HttpResponseMessage deleteResponse = await client.DeleteAsync(new Uri($"vehicles/{id}", UriKind.Relative));
    deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
  }

  [Fact]
  public async Task GetVehicles_OutsideRange_Excluded()
  {
    HttpClient client = Client(Roles.Manager);
    Guid reservationId = await SeedReservationAsync();

    HttpResponseMessage postResponse = await client.PostAsJsonAsync(
      new Uri("vehicles", UriKind.Relative), Request(reservationId));
    postResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    Guid id = await postResponse.Content.ReadFromJsonAsync<Guid>();

    HttpResponseMessage getResponse = await client.GetAsync(
      new Uri("vehicles?from=2027-01-01&to=2027-01-31", UriKind.Relative));
    getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await getResponse.Content.ReadAsStringAsync()).ShouldNotContain(id.ToString());
  }

  [Fact]
  public async Task GetVehicles_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(
      new Uri("vehicles?from=2026-06-01&to=2026-06-05", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GetVehicles_CleaningStaff_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.CleaningStaff).GetAsync(
      new Uri("vehicles?from=2026-06-01&to=2026-06-05", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }
}

public sealed class GuestsEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public GuestsEndpointTests(ApiFactory factory) => _factory = factory;

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

  private static object Request(string firstName = "Jan") => new
  {
    ReservationId = Guid.NewGuid(),
    BillId = (Guid?)null,
    FirstName = firstName,
    LastName = "Novak",
    NationalityId = Guid.NewGuid(),
    DateOfBirth = new DateOnly(1990, 1, 1),
    DocumentType = (int)DocumentType.Passport,
    DocumentNumber = "AB123456",
    Address = new Address(Guid.NewGuid(), "Brno", "60200", "Kolejni", "2"),
    ReasonOfStay = "Holiday",
    StayDateRange = new DateRange(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5)),
    VisaNumber = (string?)null,
    Note = (string?)null,
    Scartation = (DateOnly?)null,
    CheckInAt = (DateTime?)null,
    CheckOutAt = (DateTime?)null,
  };

  [Fact]
  public async Task Crud_Manager_Roundtrip_Succeeds()
  {
    HttpClient client = Client(Roles.Manager);

    HttpResponseMessage postResponse = await client.PostAsJsonAsync(
      new Uri("guests", UriKind.Relative), Request());
    postResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    Guid id = await postResponse.Content.ReadFromJsonAsync<Guid>();

    HttpResponseMessage putResponse = await client.PutAsJsonAsync(
      new Uri($"guests/{id}", UriKind.Relative), Request(firstName: "Jana"));
    putResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    HttpResponseMessage deleteResponse = await client.DeleteAsync(new Uri($"guests/{id}", UriKind.Relative));
    deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
  }

  [Fact]
  public async Task GetGuests_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(new Uri("guests", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GetGuests_CleaningStaff_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.CleaningStaff).GetAsync(new Uri("guests", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }
}
