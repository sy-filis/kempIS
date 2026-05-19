using Application.Abstractions.Authentication;
using Domain.Common;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Services.Languages;
using Infrastructure.Database;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Finance;

[Trait("Category", "Playwright")]
public sealed class GetBillPdfEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public GetBillPdfEndpointTests(ApiFactory factory) => _factory = factory;

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
  public async Task GET_ReturnsPdf_ForExistingBill()
  {
    var billId = Guid.NewGuid();
    var languageId = Guid.NewGuid();
    const string billNumber = "IT-BILL-001";

    await _factory.WithDbAsync(async db =>
    {
      db.Languages.Add(new Language { Id = languageId, Code = "en-US", Name = "English" });
      db.Bills.Add(CreateMinimalBill(billId, billNumber, languageId));
      db.BillItems.Add(new BillItem
      {
        Id = Guid.NewGuid(),
        BillId = billId,
        Quantity = 1,
        UnitPrice = 50m,
        VatRatePercentage = 21m,
      });
      await db.SaveChangesAsync();
    });

    HttpClient client = Client(Roles.Receptionist);
    HttpResponseMessage response = await client.GetAsync(new Uri($"bills/{billId}/pdf", UriKind.Relative));

    string errorMessage = _factory.ServerExceptions.TryPeek(out Exception? ex)
      ? ex.ToString()
      : "no exception";

    response.StatusCode.ShouldBe(HttpStatusCode.OK, errorMessage);
    response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf", errorMessage);

    string? disposition = response.Content.Headers.ContentDisposition?.FileNameStar
      ?? response.Content.Headers.ContentDisposition?.FileName;
    disposition.ShouldNotBeNull(errorMessage);
    disposition!.Trim('"').ShouldContain($"bill-{billNumber}.pdf");

    byte[] pdf = await response.Content.ReadAsByteArrayAsync();
    pdf.Length.ShouldBeGreaterThan(1024);
    System.Text.Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");
  }

  [Fact]
  public async Task GET_ReturnsNotFound_ForMissingBill()
  {
    HttpClient client = Client(Roles.Receptionist);
    HttpResponseMessage response = await client.GetAsync(
      new Uri($"bills/{Guid.NewGuid()}/pdf", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task GET_ReturnsUnauthorized_WithNoAuth()
  {
    HttpClient client = Client();
    HttpResponseMessage response = await client.GetAsync(
      new Uri($"bills/{Guid.NewGuid()}/pdf", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GET_ReturnsForbidden_WithWrongRole()
  {
    HttpClient client = Client(Roles.CleaningStaff);
    HttpResponseMessage response = await client.GetAsync(
      new Uri($"bills/{Guid.NewGuid()}/pdf", UriKind.Relative));

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
