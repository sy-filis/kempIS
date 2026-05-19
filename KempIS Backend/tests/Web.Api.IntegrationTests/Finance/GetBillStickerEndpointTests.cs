using System.Net;
using Application.Abstractions.Authentication;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Services.Languages;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Finance;

[Trait("Category", "Playwright")]
public sealed class GetBillStickerEndpointTests : IClassFixture<ApiFactory>
{
  private readonly ApiFactory _factory;

  public GetBillStickerEndpointTests(ApiFactory factory)
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
  public async Task GET_ReturnsStickerPdf_ForExistingBill()
  {
    await _factory.ResetAllAsync();

    var billId = Guid.NewGuid();
    var langId = Guid.NewGuid();
    const string billNumber = "STK-001";

    await _factory.WithDbAsync(async db =>
    {
      db.Languages.Add(new Language { Id = langId, Code = "cs-CZ", Name = "Česky" });
      db.Bills.Add(CreateMinimalBill(billId, billNumber, langId));
      await db.SaveChangesAsync();
    });

    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage response = await client.GetAsync($"bills/{billId}/sticker.pdf");

    string errorContext = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.OK, errorContext);
    response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");

    byte[] pdf = await response.Content.ReadAsByteArrayAsync();
    pdf.Length.ShouldBeGreaterThan(1024);
    System.Text.Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");
  }

  [Fact]
  public async Task GET_ReturnsNotFound_ForMissingBill()
  {
    await _factory.ResetAllAsync();
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage response = await client.GetAsync($"bills/{Guid.NewGuid()}/sticker.pdf");

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task GET_ReturnsUnauthorized_WithNoAuth()
  {
    HttpClient client = Client();
    HttpResponseMessage response = await client.GetAsync($"bills/{Guid.NewGuid()}/sticker.pdf");
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GET_ReturnsForbidden_WithWrongRole()
  {
    HttpClient client = Client(Roles.CleaningStaff);
    HttpResponseMessage response = await client.GetAsync($"bills/{Guid.NewGuid()}/sticker.pdf");
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
