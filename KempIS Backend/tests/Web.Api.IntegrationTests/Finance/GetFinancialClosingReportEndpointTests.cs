using System.Net;
using Application.Abstractions.Authentication;
using Domain.Common;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.FinancialClosings;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Services.Services;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Finance;

[Trait("Category", "Playwright")]
public sealed class GetFinancialClosingReportEndpointTests : IClassFixture<PlaywrightApiFactory>
{
  private readonly PlaywrightApiFactory _factory;

  public GetFinancialClosingReportEndpointTests(PlaywrightApiFactory factory)
  {
    _factory = factory;
  }

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
  public async Task GET_ReturnsPdf_AndCachesOnSecondCall()
  {
    var closingId = Guid.NewGuid();
    uint sequentialNumber = 42;

    await _factory.ResetAllAsync();

    await _factory.WithDbAsync(async db =>
    {
      db.FinancialClosings.Add(new FinancialClosing
      {
        Id = closingId,
        FinancialClosingId = sequentialNumber,
        ClosedAtUtc = new DateTime(2026, 4, 21, 18, 0, 0, DateTimeKind.Utc),
        TotalAmount = 1000m,
      });

      var billId = Guid.NewGuid();
      var serviceId = Guid.NewGuid();

      db.Services.Add(new Service
      {
        Id = serviceId,
        ServiceGroup = ServiceGroup.Spots,
        ServiceTypeId = Guid.NewGuid(),
        VatRateId = Guid.NewGuid(),
        Name = "Spot",
        IsActive = true,
      });

      Bill bill = CreateMinimalBill(billId, "B-001", Guid.NewGuid());
      bill.FinancialClosingId = closingId;
      db.Bills.Add(bill);

      db.BillItems.Add(new BillItem
      {
        Id = Guid.NewGuid(),
        BillId = billId,
        ServiceId = serviceId,
        Quantity = 1,
        UnitPrice = 1000m,
        VatRatePercentage = 10m,
      });

      await db.SaveChangesAsync();
    });

    HttpClient client = Client(Roles.Accountant);

    HttpResponseMessage first = await client.GetAsync($"financial-closings/{closingId}/pdf");

    string errorContext = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    first.StatusCode.ShouldBe(HttpStatusCode.OK, errorContext);
    first.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
    first.Content.Headers.ContentDisposition?.FileName!.ShouldContain($"financial-closing-{sequentialNumber}.pdf");

    byte[] firstBytes = await first.Content.ReadAsByteArrayAsync();
    firstBytes.Length.ShouldBeGreaterThan(1024);
    System.Text.Encoding.ASCII.GetString(firstBytes, 0, 5).ShouldBe("%PDF-");

    HttpResponseMessage second = await client.GetAsync($"financial-closings/{closingId}/pdf");
    second.StatusCode.ShouldBe(HttpStatusCode.OK);
    byte[] secondBytes = await second.Content.ReadAsByteArrayAsync();

    secondBytes.ShouldBe(firstBytes);
  }

  [Fact]
  public async Task GET_ReturnsNotFound_ForMissingClosing()
  {
    await _factory.ResetAllAsync();
    HttpClient client = Client(Roles.Accountant);

    HttpResponseMessage response = await client.GetAsync($"financial-closings/{Guid.NewGuid()}/pdf");

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task GET_ReturnsUnauthorized_WithNoAuth()
  {
    HttpClient client = Client();
    HttpResponseMessage response = await client.GetAsync($"financial-closings/{Guid.NewGuid()}/pdf");
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GET_ReturnsForbidden_WithWrongRole()
  {
    HttpClient client = Client(Roles.CleaningStaff);
    HttpResponseMessage response = await client.GetAsync($"financial-closings/{Guid.NewGuid()}/pdf");
    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  private static Bill CreateMinimalBill(Guid id, string number, Guid languageId) =>
    new()
    {
      Id = id,
      Number = number,
      ReservationId = Guid.NewGuid(),
      IssuedAtUtc = DateTime.UtcNow,
      CheckInAt = new DateOnly(2026, 4, 20),
      CheckOutAt = new DateOnly(2026, 4, 22),
      LanguageIdGuid = languageId,
      Payer = new Payer
      {
        Name = "John",
        Surname = "Doe",
        Address = new Address(Guid.NewGuid(), "Prague", "10000", "Main St", "1"),
      },
      LegalEntity = new LegalEntity
      {
        Name = "Acme s.r.o.",
        Address = new Address(Guid.NewGuid(), "Prague", "10000", "Main St", "1"),
        Cin = "12345678",
        Tin = "CZ12345678",
      },
      Payment = new Payment(PaymentType.Cash, 100m),
    };
}
