using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Reservations.Guests;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reservations.Guests;

public sealed class GetGuestsEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public GetGuestsEndpointTests(ApiFactory factory) => _factory = factory;

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

  private static Address Addr(string city = "Prague") => new(Guid.NewGuid(), city, "10000", "Main", "1");

  private static Bill MakeBill(Guid id, DateOnly checkIn, DateOnly checkOut) => new()
  {
    Id = id,
    Number = "B-" + id.ToString("N")[..6],
    Kind = BillKind.Regular,
    ReservationId = Guid.NewGuid(),
    LanguageIdGuid = Guid.NewGuid(),
    IssuedAtUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
    CheckInAt = checkIn,
    CheckOutAt = checkOut,
    Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    LegalEntity = new LegalEntity { Name = "L", Cin = "1", Tin = "1", Address = Addr() },
    Payment = new Payment(PaymentType.Cash, 100m),
  };

  private static Guest MakeGuest(Guid? billId, string lastName = "Doe", string city = "Prague") => new()
  {
    Id = Guid.NewGuid(),
    ReservationId = Guid.NewGuid(),
    BillId = billId,
    FirstName = "John",
    LastName = lastName,
    NationalityId = Guid.NewGuid(),
    DateOfBirth = new DateOnly(1990, 1, 1),
    DocumentType = DocumentType.Passport,
    DocumentNumber = "X1",
    Address = Addr(city),
    ReasonOfStay = "tourism",
    StayDateRange = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
  };

  [Fact]
  public async Task GetGuests_AnonymousRequest_Returns401()
  {
    HttpResponseMessage response = await _factory.CreateClient()
      .GetAsync("guests?from=2026-05-01&to=2026-05-31");

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GetGuests_AuthenticatedAsNonAllowedRole_Returns403()
  {
    HttpResponseMessage response = await Client("Accountant")
      .GetAsync("guests?from=2026-05-01&to=2026-05-31");

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task GetGuests_MissingFrom_Returns400()
  {
    HttpResponseMessage response = await Client("Receptionist")
      .GetAsync("guests?to=2026-05-31");

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task GetGuests_FromGreaterThanTo_Returns400()
  {
    HttpResponseMessage response = await Client("Receptionist")
      .GetAsync("guests?from=2026-05-31&to=2026-05-01");

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task GetGuests_BillOverlap_ReturnsMatchingGuests()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));
    var guestId = Guid.NewGuid();

    await _factory.WithDbAsync(async db =>
    {
      db.Bills.Add(bill);
      Guest g = MakeGuest(bill.Id);
      g.Id = guestId;
      db.Guests.Add(g);
      db.Guests.Add(MakeGuest(billId: null));
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client("Receptionist")
      .GetAsync("guests?from=2026-05-01&to=2026-05-31");

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    List<Application.Reservations.Guests.GuestResponse>? guests =
      await response.Content.ReadFromJsonAsync<List<Application.Reservations.Guests.GuestResponse>>();
    guests.ShouldNotBeNull();
    guests.Count.ShouldBe(1);
    guests[0].Id.ShouldBe(guestId);
  }

  [Fact]
  public async Task GetGuests_SearchNarrowsResults()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));

    await _factory.WithDbAsync(async db =>
    {
      db.Bills.Add(bill);
      db.Guests.Add(MakeGuest(bill.Id, lastName: "Novak"));
      db.Guests.Add(MakeGuest(bill.Id, lastName: "Svoboda"));
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client("Receptionist")
      .GetAsync("guests?from=2026-05-01&to=2026-05-31&search=novak");

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    List<Application.Reservations.Guests.GuestResponse>? guests =
      await response.Content.ReadFromJsonAsync<List<Application.Reservations.Guests.GuestResponse>>();
    guests.ShouldNotBeNull();
    guests.Count.ShouldBe(1);
    guests[0].LastName.ShouldBe("Novak");
  }
}
