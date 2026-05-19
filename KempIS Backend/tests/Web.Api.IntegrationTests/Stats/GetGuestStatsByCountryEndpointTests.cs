using Application.Abstractions.Authentication;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Reservations.Guests;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Stats;

public sealed class GetGuestStatsByCountryEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public GetGuestStatsByCountryEndpointTests(ApiFactory factory) => _factory = factory;

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
      new Uri("stats/guests/by-country?from=2026-06-01&to=2026-08-31", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Get_WrongRole_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.CleaningStaff).GetAsync(
      new Uri("stats/guests/by-country?from=2026-06-01&to=2026-08-31", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Get_ToBeforeFrom_Returns400()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("stats/guests/by-country?from=2026-08-01&to=2026-07-01", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Get_RangeTooLarge_Returns400()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("stats/guests/by-country?from=2026-01-01&to=2027-01-02", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  // Seeded nationality IDs from Infrastructure.Seed.Countries - stable, do not change.
  private static readonly Guid CzeId = new("afe4ab41-6a08-46b2-a1c2-8477a0b95404");
  private static readonly Guid SvkId = new("0af06091-c293-40f8-806a-3e5c33b9f09c");

  [Fact]
  public async Task Get_HappyPath_ReturnsGroupedCountsAndNights()
  {
    var czBill = Guid.NewGuid();
    var skBill = Guid.NewGuid();

    await _factory.WithDbAsync(async db =>
    {
      db.Bills.Add(MakeBill(czBill, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 6)));
      db.Bills.Add(MakeBill(skBill, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 4)));
      db.Guests.Add(MakeGuest(CzeId, czBill));
      db.Guests.Add(MakeGuest(CzeId, czBill));
      db.Guests.Add(MakeGuest(SvkId, skBill));
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("stats/guests/by-country?from=2026-06-01&to=2026-08-31", UriKind.Relative));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    GuestStatsByCountryDto? body =
      await response.Content.ReadFromJsonAsync<GuestStatsByCountryDto>();
    body.ShouldNotBeNull();
    body.TotalGuests.ShouldBe(3);
    body.TotalPersonNights.ShouldBe(13);
    body.Rows.Count.ShouldBe(2);
    body.Rows[0].Alpha3.ShouldBe("CZE");
    body.Rows[0].PersonNights.ShouldBe(10);
    body.Rows[1].Alpha3.ShouldBe("SVK");
    body.Rows[1].PersonNights.ShouldBe(3);
  }

  private static Bill MakeBill(Guid id, DateOnly checkIn, DateOnly checkOut) => new()
  {
    Id = id,
    Number = "B-" + id.ToString("N")[..6],
    ReservationId = Guid.NewGuid(),
    IssuedAtUtc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc),
    CheckInAt = checkIn,
    CheckOutAt = checkOut,
    LanguageIdGuid = Guid.NewGuid(),
    Payer = new Payer
    {
      Name = "Jan",
      Surname = "Novak",
      Address = new Address(Guid.NewGuid(), "Prague", "10000", "Main", "1"),
    },
    LegalEntity = new LegalEntity
    {
      Name = "Acme",
      Address = new Address(Guid.NewGuid(), "Prague", "10000", "Main", "1"),
      Cin = "12345678",
      Tin = "CZ12345678",
    },
    Payment = new Payment(PaymentType.Cash, 0m),
  };

  private static Guest MakeGuest(Guid nationalityId, Guid billId) => new()
  {
    Id = Guid.NewGuid(),
    BillId = billId,
    ReservationId = Guid.NewGuid(),
    FirstName = "Anna",
    LastName = "Tester",
    NationalityId = nationalityId,
    DateOfBirth = new DateOnly(2000, 1, 1),
    Address = new Address(nationalityId, "Prague", "10000", "Main", "1"),
    ReasonOfStay = "Tourism",
  };

  private sealed record GuestStatsByCountryDto(
    DateOnly From, DateOnly To,
    int TotalGuests, int TotalPersonNights,
    IReadOnlyList<GuestStatsByCountryRowDto> Rows);

  private sealed record GuestStatsByCountryRowDto(
    Guid NationalityId, string Alpha2, string Alpha3, string Name, string NameEn,
    int GuestCount, int PersonNights);
}
