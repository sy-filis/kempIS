using Application.Abstractions.Authentication;
using Application.Reservations.Vehicles;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Reservations.Vehicles;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Reservations.Vehicles;

public sealed class GetVehiclesEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public GetVehiclesEndpointTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync() => _factory.ResetAllAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  private HttpClient Client(params string[] roles)
  {
    HttpClient c = _factory.CreateClient();
    if (roles.Length > 0)
    {
      c.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
    }
    return c;
  }

  private static Address Addr() => new(Guid.NewGuid(), "Prague", "10000", "Main", "1");

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

  private static Vehicle MakeVehicle(Guid? billId, string plate = "1AB2345") => new()
  {
    Id = Guid.NewGuid(),
    ReservationId = Guid.NewGuid(),
    BillId = billId,
    ServiceId = Guid.NewGuid(),
    RegistrationNumber = plate,
  };

  [Fact]
  public async Task GetVehicles_AnonymousRequest_Returns401()
  {
    HttpResponseMessage response = await _factory.CreateClient()
      .GetAsync("vehicles?from=2026-05-01&to=2026-05-31");

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GetVehicles_AuthenticatedAsNonAllowedRole_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.Accountant)
      .GetAsync("vehicles?from=2026-05-01&to=2026-05-31");

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task GetVehicles_FromGreaterThanTo_Returns400()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist)
      .GetAsync("vehicles?from=2026-05-31&to=2026-05-01");

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task GetVehicles_BillOverlap_ReturnsMatchingVehicles()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));

    await _factory.WithDbAsync(async db =>
    {
      db.Bills.Add(bill);
      db.Vehicles.Add(MakeVehicle(bill.Id, plate: "1AB2345"));
      db.Vehicles.Add(MakeVehicle(billId: null, plate: "9NO9999"));
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist)
      .GetAsync("vehicles?from=2026-05-01&to=2026-05-31");

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    List<VehicleResponse>? vehicles =
      await response.Content.ReadFromJsonAsync<List<VehicleResponse>>();
    vehicles.ShouldNotBeNull();
    vehicles.Count.ShouldBe(1);
    vehicles[0].RegistrationNumber.ShouldBe("1AB2345");
  }

  [Fact]
  public async Task GetVehicles_SearchNarrowsResults()
  {
    Bill bill = MakeBill(Guid.NewGuid(), new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 15));

    await _factory.WithDbAsync(async db =>
    {
      db.Bills.Add(bill);
      db.Vehicles.Add(MakeVehicle(bill.Id, plate: "1AB2345"));
      db.Vehicles.Add(MakeVehicle(bill.Id, plate: "9XY9999"));
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist)
      .GetAsync("vehicles?from=2026-05-01&to=2026-05-31&search=1AB");

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    List<VehicleResponse>? vehicles =
      await response.Content.ReadFromJsonAsync<List<VehicleResponse>>();
    vehicles.ShouldNotBeNull();
    vehicles.Count.ShouldBe(1);
    vehicles[0].RegistrationNumber.ShouldBe("1AB2345");
  }
}
