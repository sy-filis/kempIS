using Domain.Common;
using Domain.Reservations.Guests;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reservations.Guests;

public sealed class InCampGuestCountEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public InCampGuestCountEndpointTests(ApiFactory factory) => _factory = factory;

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

  private static Guest BuildGuest(DateTime? checkInAt, DateTime? checkOutAt) => new()
  {
    Id = Guid.NewGuid(),
    ReservationId = Guid.NewGuid(),
    FirstName = "A",
    LastName = "B",
    NationalityId = Guid.NewGuid(),
    DateOfBirth = new DateOnly(1990, 1, 1),
    DocumentType = DocumentType.Passport,
    DocumentNumber = "X",
    Address = new Address(Guid.NewGuid(), "City", "12345", "Street", "1"),
    ReasonOfStay = "tourism",
    StayDateRange = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
    CheckInAt = checkInAt,
    CheckOutAt = checkOutAt,
  };

  [Fact]
  public async Task GetInCampGuestCount_AnonymousRequest_Returns401()
  {
    HttpResponseMessage response = await _factory.CreateClient().GetAsync("guests/in-camp-count");

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GetInCampGuestCount_ReturnsCountOfGuestsCheckedInAndNotCheckedOut()
  {
    DateTime t = new(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc);

    await _factory.WithDbAsync(async db =>
    {
      db.Guests.Add(BuildGuest(checkInAt: null, checkOutAt: null));
      db.Guests.Add(BuildGuest(checkInAt: t, checkOutAt: t.AddHours(1)));
      db.Guests.Add(BuildGuest(checkInAt: t, checkOutAt: null));
      db.Guests.Add(BuildGuest(checkInAt: t.AddDays(-1), checkOutAt: null));
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client("Receptionist")
      .GetAsync("guests/in-camp-count");

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
    int count = await response.Content.ReadFromJsonAsync<int>();
    count.ShouldBe(2);
  }

  [Fact]
  public async Task GetInCampGuestCount_AuthenticatedAsNonAllowedRole_Returns403()
  {
    HttpResponseMessage response = await Client("Accountant")
      .GetAsync("guests/in-camp-count");

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }
}
