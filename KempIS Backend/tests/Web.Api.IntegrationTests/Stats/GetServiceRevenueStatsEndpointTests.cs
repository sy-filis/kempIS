using Application.Abstractions.Authentication;
using Domain.Common;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Services.Services;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Stats;

public sealed class GetServiceRevenueStatsEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public GetServiceRevenueStatsEndpointTests(ApiFactory factory) => _factory = factory;

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
      new Uri("stats/services?from=2026-06-01&to=2026-08-31", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Get_WrongRole_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.CleaningStaff).GetAsync(
      new Uri("stats/services?from=2026-06-01&to=2026-08-31", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Get_ToBeforeFrom_Returns400()
  {
    HttpResponseMessage response = await Client(Roles.Manager).GetAsync(
      new Uri("stats/services?from=2026-08-01&to=2026-07-01", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Get_RangeTooLarge_Returns400()
  {
    HttpResponseMessage response = await Client(Roles.Manager).GetAsync(
      new Uri("stats/services?from=2026-01-01&to=2027-01-02", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Get_HappyPath_ReturnsNestedGroups()
  {
    var billId = Guid.NewGuid();
    var spotsServiceId = Guid.NewGuid();
    var mealsServiceId = Guid.NewGuid();

    await _factory.WithDbAsync(async db =>
    {
      db.Services.Add(MakeService(spotsServiceId, ServiceGroup.Spots, "Pitch"));
      db.Services.Add(MakeService(mealsServiceId, ServiceGroup.Meals, "Breakfast"));
      db.Bills.Add(MakeBill(billId, new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc)));
      db.BillItems.Add(MakeItem(billId, spotsServiceId, qty: 4, unitPrice: 100m, vatPct: 21m));
      db.BillItems.Add(MakeItem(billId, mealsServiceId, qty: 2, unitPrice: 50m, vatPct: 12m));
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Manager).GetAsync(
      new Uri("stats/services?from=2026-06-01&to=2026-08-31", UriKind.Relative));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    ServiceRevenueStatsDto? body = await response.Content.ReadFromJsonAsync<ServiceRevenueStatsDto>();
    body.ShouldNotBeNull();
    body.Groups.Count.ShouldBe(2);
    body.Groups[0].ServiceGroup.ShouldBe("Spots");
    body.Groups[0].Services.Count.ShouldBe(1);
    body.Groups[0].Services[0].ServiceName.ShouldBe("Pitch");
    body.Groups[0].GroupGross.ShouldBe(body.Groups[0].GroupNet + body.Groups[0].GroupVat);
  }

  private static Service MakeService(Guid id, ServiceGroup group, string name) => new()
  {
    Id = id,
    Name = name,
    ServiceGroup = group,
    ServiceTypeId = Guid.NewGuid(),
    VatRateId = Guid.NewGuid(),
    BasePrice = 0m,
    IsActive = true,
  };

  private static Bill MakeBill(Guid id, DateTime issuedAtUtc) => new()
  {
    Id = id,
    Number = "B-" + id.ToString("N")[..6],
    ReservationId = Guid.NewGuid(),
    IssuedAtUtc = issuedAtUtc,
    CheckInAt = new DateOnly(2026, 6, 1),
    CheckOutAt = new DateOnly(2026, 6, 2),
    LanguageIdGuid = Guid.NewGuid(),
    Payer = new Payer
    {
      Name = "J",
      Surname = "N",
      Address = new Address(Guid.NewGuid(), "P", "10000", "S", "1"),
    },
    LegalEntity = new LegalEntity
    {
      Name = "Acme",
      Address = new Address(Guid.NewGuid(), "P", "10000", "S", "1"),
      Cin = "12345678",
      Tin = "CZ12345678",
    },
    Payment = new Payment(PaymentType.Cash, 0m),
  };

  private static BillItem MakeItem(Guid billId, Guid serviceId, uint qty, decimal unitPrice, decimal vatPct) => new()
  {
    Id = Guid.NewGuid(),
    BillId = billId,
    ServiceId = serviceId,
    Quantity = qty,
    UnitPrice = unitPrice,
    VatRatePercentage = vatPct,
    RecapSingleQuantity = qty,
    RecapDayQuantity = 1,
  };

  private sealed record ServiceRevenueStatsDto(
    DateOnly From, DateOnly To,
    decimal TotalNet, decimal TotalVat, decimal TotalGross,
    IReadOnlyList<ServiceRevenueGroupDto> Groups);

  private sealed record ServiceRevenueGroupDto(
    string ServiceGroup,
    decimal GroupNet, decimal GroupVat, decimal GroupGross,
    IReadOnlyList<ServiceRevenueRowDto> Services);

  private sealed record ServiceRevenueRowDto(
    Guid ServiceId, string ServiceName, bool IsActive,
    decimal VatRatePercentage, long Quantity,
    decimal Net, decimal Vat, decimal Gross);
}
