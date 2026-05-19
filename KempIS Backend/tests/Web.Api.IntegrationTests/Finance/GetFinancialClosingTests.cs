using Application.Abstractions.Authentication;
using Application.Finance.FinancialClosings.GetFinancialClosing;
using Domain.Common;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.FinancialClosings;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Services.Services;
using Domain.Services.ServiceTypes;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Finance;

public sealed class GetFinancialClosingTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public GetFinancialClosingTests(ApiFactory factory) => _factory = factory;

  public async Task InitializeAsync()
  {
    await _factory.ResetAllAsync();
    await _factory.WithDbAsync(async db =>
    {
      db.BillItems.RemoveRange(db.BillItems);
      db.Bills.RemoveRange(db.Bills);
      db.FinancialClosings.RemoveRange(db.FinancialClosings);
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

  [Fact]
  public async Task Get_ReturnsUnauthorized_ForAnonymous()
  {
    HttpClient client = Client();
    HttpResponseMessage response = await client.GetAsync($"financial-closings/{Guid.NewGuid()}");
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Get_ReturnsForbidden_ForCleaningStaff()
  {
    HttpClient client = Client(Roles.CleaningStaff);
    HttpResponseMessage response = await client.GetAsync($"financial-closings/{Guid.NewGuid()}");
    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Get_ReturnsNotFound_ForUnknownId()
  {
    HttpClient client = Client(Roles.Receptionist);
    HttpResponseMessage response = await client.GetAsync($"financial-closings/{Guid.NewGuid()}");
    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Get_ReturnsDetail_ForReceptionist()
  {
    var closingId = Guid.NewGuid();
    var cashBillId = Guid.NewGuid();
    var cardBillId = Guid.NewGuid();
    var spotTypeId = Guid.NewGuid();
    var drinkTypeId = Guid.NewGuid();
    var spotServiceId = Guid.NewGuid();
    var drinkServiceId = Guid.NewGuid();
    var closedAt = new DateTime(2026, 5, 11, 20, 0, 0, DateTimeKind.Utc);
    var cashAt = new DateTime(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc);
    var cardAt = new DateTime(2026, 5, 11, 9, 0, 0, DateTimeKind.Utc);

    await _factory.WithDbAsync(async db =>
    {
      db.FinancialClosings.Add(new FinancialClosing
      {
        Id = closingId,
        FinancialClosingId = 42u,
        ClosedAtUtc = closedAt,
        TotalAmount = 1138m,
      });
      db.ServiceTypes.Add(new ServiceType { Id = spotTypeId, Name = "Spot fees", IsActive = true });
      db.ServiceTypes.Add(new ServiceType { Id = drinkTypeId, Name = "Drinks", IsActive = true });
      db.Services.Add(new Service
      {
        Id = spotServiceId,
        ServiceGroup = ServiceGroup.Spots,
        ServiceTypeId = spotTypeId,
        VatRateId = Guid.NewGuid(),
        Name = "Spot",
        BasePrice = 100m,
        IsActive = true,
      });
      db.Services.Add(new Service
      {
        Id = drinkServiceId,
        ServiceGroup = ServiceGroup.Meals,
        ServiceTypeId = drinkTypeId,
        VatRateId = Guid.NewGuid(),
        Name = "Beer",
        BasePrice = 50m,
        IsActive = true,
      });
      db.Bills.Add(BuildBill(cashBillId, closingId, "2026-0042", cashAt, "Jan", "Novák", PaymentType.Cash, 896m));
      db.Bills.Add(BuildBill(cardBillId, closingId, "2026-0043", cardAt, "Eva", "Dvořáková", PaymentType.Card, 242m));
      db.BillItems.Add(new BillItem
      {
        Id = Guid.NewGuid(),
        BillId = cashBillId,
        ServiceId = spotServiceId,
        Quantity = 8,
        UnitPrice = 112m,
        VatRatePercentage = 12m,
        RecapSingleQuantity = 8,
        RecapDayQuantity = 1,
      });
      db.BillItems.Add(new BillItem
      {
        Id = Guid.NewGuid(),
        BillId = cardBillId,
        ServiceId = drinkServiceId,
        Quantity = 2,
        UnitPrice = 121m,
        VatRatePercentage = 21m,
        RecapSingleQuantity = 2,
        RecapDayQuantity = 1,
      });
      await db.SaveChangesAsync();
    });

    HttpClient client = Client(Roles.Receptionist);
    HttpResponseMessage response = await client.GetAsync($"financial-closings/{closingId}");

    string errorContext = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.OK, errorContext);

    string rawJson = await response.Content.ReadAsStringAsync();
    rawJson.ShouldContain("\"paymentType\":0");
    rawJson.ShouldContain("\"paymentType\":1");
    rawJson.ShouldContain("\"kind\":0");
    rawJson.ShouldContain("\"paymentTotals\"");
    rawJson.ShouldContain("\"vatRecap\"");
    rawJson.ShouldContain("\"vatRecapByServiceType\"");

    FinancialClosingDetailResponse? body =
      await response.Content.ReadFromJsonAsync<FinancialClosingDetailResponse>();
    body.ShouldNotBeNull();
    body!.Id.ShouldBe(closingId);
    body.FinancialClosingId.ShouldBe(42u);
    body.ClosedAtUtc.ShouldBe(closedAt);
    body.Bills.Count.ShouldBe(2);
    body.Bills.Select(b => b.Id).ShouldBe([cashBillId, cardBillId]);
    body.Bills[0].PayerName.ShouldBe("Jan Novák");
    body.Bills[0].PaymentType.ShouldBe(PaymentType.Cash);
    body.Bills[1].PaymentType.ShouldBe(PaymentType.Card);
    body.PaymentTotals.Cash.ShouldBe(896m);
    body.PaymentTotals.Card.ShouldBe(242m);
    body.PaymentTotals.Total.ShouldBe(1138m);
    body.VatRecap.Count.ShouldBe(2);
    body.VatRecap.Select(r => r.VatRatePercentage).ShouldBe([12m, 21m]);
    body.VatRecap.Sum(r => r.Gross).ShouldBe(1138m);
    body.VatRecapByServiceType.Count.ShouldBe(2);
    body.VatRecapByServiceType.Select(r => r.ServiceTypeName).ShouldBe(["Drinks", "Spot fees"]);
  }

  private static Bill BuildBill(
    Guid id,
    Guid closingId,
    string number,
    DateTime issuedAtUtc,
    string payerName,
    string payerSurname,
    PaymentType paymentType,
    decimal amount)
    => new()
    {
      Id = id,
      Number = number,
      ReservationId = Guid.NewGuid(),
      IssuedAtUtc = issuedAtUtc,
      CheckInAt = new DateOnly(2026, 5, 1),
      CheckOutAt = new DateOnly(2026, 5, 2),
      LanguageIdGuid = Guid.NewGuid(),
      FinancialClosingId = closingId,
      Payer = new Payer
      {
        Name = payerName,
        Surname = payerSurname,
        Address = new Address(Guid.NewGuid(), "Prague", "10000", "Main", "1"),
      },
      LegalEntity = new LegalEntity
      {
        Name = "Acme",
        Address = new Address(Guid.NewGuid(), "Prague", "10000", "Main", "1"),
        Cin = "12345678",
        Tin = "CZ12345678",
      },
      Payment = new Payment(paymentType, amount),
    };
}
