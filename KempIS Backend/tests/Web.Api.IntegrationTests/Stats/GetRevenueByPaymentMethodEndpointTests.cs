using Application.Abstractions.Authentication;
using Domain.Common;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Stats;

public sealed class GetRevenueByPaymentMethodEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public GetRevenueByPaymentMethodEndpointTests(ApiFactory factory) => _factory = factory;

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
      new Uri("stats/revenue/by-payment-method?from=2026-06-01&to=2026-08-31", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Get_WrongRole_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.CleaningStaff).GetAsync(
      new Uri("stats/revenue/by-payment-method?from=2026-06-01&to=2026-08-31", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Get_ToBeforeFrom_Returns400()
  {
    HttpResponseMessage response = await Client(Roles.Manager).GetAsync(
      new Uri("stats/revenue/by-payment-method?from=2026-08-01&to=2026-07-01", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Get_RangeTooLarge_Returns400()
  {
    HttpResponseMessage response = await Client(Roles.Manager).GetAsync(
      new Uri("stats/revenue/by-payment-method?from=2026-01-01&to=2027-01-02", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Get_HappyPath_ReturnsBothRows()
  {
    var cashBill = Guid.NewGuid();
    var cardBill = Guid.NewGuid();

    await _factory.WithDbAsync(async db =>
    {
      db.Bills.Add(MakeBill(cashBill, new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), PaymentType.Cash));
      db.Bills.Add(MakeBill(cardBill, new DateTime(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc), PaymentType.Card));
      // UnitPrice is VAT-inclusive (gross). Row gross = qty × UnitPrice.
      db.BillItems.Add(MakeItem(cashBill, qty: 1, unitPrice: 100m, vatPct: 21m));  // 100
      db.BillItems.Add(MakeItem(cardBill, qty: 1, unitPrice: 200m, vatPct: 21m));  // 200
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Manager).GetAsync(
      new Uri("stats/revenue/by-payment-method?from=2026-06-01&to=2026-08-31", UriKind.Relative));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    RevenueByPaymentMethodDto? body = await response.Content.ReadFromJsonAsync<RevenueByPaymentMethodDto>();
    body.ShouldNotBeNull();
    body.TotalBillCount.ShouldBe(2);
    body.TotalGross.ShouldBe(300m);
    body.Rows.Count.ShouldBe(2);
    body.Rows[0].PaymentType.ShouldBe("Card");   // 200 > 100
    body.Rows[0].Gross.ShouldBe(200m);
    body.Rows[1].PaymentType.ShouldBe("Cash");
    body.Rows[1].Gross.ShouldBe(100m);
  }

  private static Bill MakeBill(Guid id, DateTime issuedAtUtc, PaymentType payment) => new()
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
    Payment = new Payment(payment, 0m),
  };

  private static BillItem MakeItem(Guid billId, uint qty, decimal unitPrice, decimal vatPct) => new()
  {
    Id = Guid.NewGuid(),
    BillId = billId,
    ServiceId = Guid.NewGuid(),
    Quantity = qty,
    UnitPrice = unitPrice,
    VatRatePercentage = vatPct,
    RecapSingleQuantity = qty,
    RecapDayQuantity = 1,
  };

  private sealed record RevenueByPaymentMethodDto(
    DateOnly From, DateOnly To,
    int TotalBillCount, decimal TotalGross,
    IReadOnlyList<RevenueByPaymentMethodRowDto> Rows);

  private sealed record RevenueByPaymentMethodRowDto(
    string PaymentType, int BillCount, decimal Gross, decimal SharePercent);
}
