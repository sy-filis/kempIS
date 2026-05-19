using Application.Abstractions.Authentication;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Reservations.Vehicles;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reservations;

public sealed class VehicleLookupEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public VehicleLookupEndpointTests(ApiFactory factory) => _factory = factory;

  public async Task InitializeAsync()
  {
    await _factory.ResetAllAsync();
    // ResetAllAsync does not clear Bills; flush any leftovers so each test starts clean.
    await _factory.WithDbAsync(async db =>
    {
      db.Bills.RemoveRange(db.Bills);
      await db.SaveChangesAsync();
    });
  }

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

  // ApiFactory does not substitute IDateTimeProvider, so the running app uses real
  // DateTime.UtcNow. Tests seed bills relative to "today" computed the same way.
  private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow.Date);

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

  private static Bill MakeBill(DateOnly checkOut) => new()
  {
    Id = Guid.NewGuid(),
    Number = "B-" + Guid.NewGuid().ToString("N")[..6],
    Kind = BillKind.Regular,
    ReservationId = Guid.NewGuid(),
    LanguageIdGuid = Guid.NewGuid(),
    IssuedAtUtc = DateTime.UtcNow,
    CheckInAt = checkOut.AddDays(-3),
    CheckOutAt = checkOut,
    Payer = new Payer { Name = "A", Surname = "B", Address = Addr() },
    LegalEntity = new LegalEntity { Name = "L", Cin = "1", Tin = "1", Address = Addr() },
    Payment = new Payment(PaymentType.Cash, 100m),
  };

  private async Task SeedBilledVehicleAsync(string plate, DateOnly checkOut)
  {
    await _factory.WithDbAsync(async db =>
    {
      Bill bill = MakeBill(checkOut);
      db.Bills.Add(bill);
      db.Vehicles.Add(new Vehicle
      {
        Id = Guid.NewGuid(),
        ReservationId = Guid.NewGuid(),
        BillId = bill.Id,
        ServiceId = Guid.NewGuid(),
        RegistrationNumber = plate,
      });
      await db.SaveChangesAsync();
    });
  }

  private sealed record LookupBody(string LicencePlate);
  private sealed record LookupResult(string LicencePlate, DateOnly CheckoutAt);

  [Fact]
  public async Task Post_Match_Returns200WithNormalizedPlateAndCheckoutAt()
  {
    DateOnly checkOut = Today().AddDays(2);
    await SeedBilledVehicleAsync("1AB-2345", checkOut);

    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("vehicles/lookup", UriKind.Relative),
      new LookupBody("1ab 2345"));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    LookupResult? body = await response.Content.ReadFromJsonAsync<LookupResult>();
    body.ShouldNotBeNull();
    body.LicencePlate.ShouldBe("1AB2345");
    body.CheckoutAt.ShouldBe(checkOut);
  }

  [Fact]
  public async Task Post_VehicleWithoutBill_Returns404()
  {
    await _factory.WithDbAsync(async db =>
    {
      db.Vehicles.Add(new Vehicle
      {
        Id = Guid.NewGuid(),
        ReservationId = Guid.NewGuid(),
        BillId = null,
        ServiceId = Guid.NewGuid(),
        RegistrationNumber = "1AB2345",
      });
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("vehicles/lookup", UriKind.Relative),
      new LookupBody("1AB2345"));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Post_BillExpired_Returns404()
  {
    await SeedBilledVehicleAsync("1AB2345", Today().AddDays(-1));

    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("vehicles/lookup", UriKind.Relative),
      new LookupBody("1AB2345"));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Post_NoVehicleAtAll_Returns404()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("vehicles/lookup", UriKind.Relative),
      new LookupBody("ZZZ9999"));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Post_TwoMatches_ReturnsLatestCheckout()
  {
    DateOnly earlier = Today().AddDays(1);
    DateOnly later = Today().AddDays(5);
    await SeedBilledVehicleAsync("1AB2345", earlier);
    await SeedBilledVehicleAsync("1AB-2345", later);

    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("vehicles/lookup", UriKind.Relative),
      new LookupBody("1AB2345"));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    LookupResult? body = await response.Content.ReadFromJsonAsync<LookupResult>();
    body!.CheckoutAt.ShouldBe(later);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("---")]
  public async Task Post_EmptyOrJunkPlate_Returns400(string plate)
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).PostAsJsonAsync(
      new Uri("vehicles/lookup", UriKind.Relative),
      new LookupBody(plate));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Post_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().PostAsJsonAsync(
      new Uri("vehicles/lookup", UriKind.Relative),
      new LookupBody("1AB2345"));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Post_AnyAuthenticatedRole_IsAllowed()
  {
    DateOnly checkOut = Today().AddDays(1);
    await SeedBilledVehicleAsync("XYZ123", checkOut);

    // CleaningStaff is rejected by the existing /vehicles group; for the lookup
    // endpoint we expect 200 - proving auth is "any authenticated user".
    HttpResponseMessage response = await Client(Roles.CleaningStaff).PostAsJsonAsync(
      new Uri("vehicles/lookup", UriKind.Relative),
      new LookupBody("xyz123"));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
  }
}
