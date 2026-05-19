using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Authentication;
using Domain.Common;
using Domain.Finance.BillItems;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Finance;

public sealed class CreateRepairBillEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public CreateRepairBillEndpointTests(ApiFactory factory) => _factory = factory;

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

  private async Task<(Guid originalId, Guid serviceId)> SeedOriginalBill(uint quantity = 3u)
  {
    var id = Guid.NewGuid();
    var serviceId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      var payer = new Payer
      {
        Name = "John",
        Surname = "Doe",
        Address = new Address(Guid.NewGuid(), "Prague", "10000", "Main", "1"),
      };
      var legal = new LegalEntity
      {
        Name = "Acme",
        Cin = "123",
        Tin = "CZ123",
        Address = new Address(Guid.NewGuid(), "Prague", "10000", "Main", "1"),
      };
      db.Bills.Add(new Bill
      {
        Id = id,
        Number = $"IT-{Guid.NewGuid():N}"[..10],
        Kind = BillKind.Regular,
        ReservationId = Guid.NewGuid(),
        LanguageIdGuid = Guid.NewGuid(),
        IssuedAtUtc = DateTime.UtcNow,
        CheckInAt = new DateOnly(2026, 4, 20),
        CheckOutAt = new DateOnly(2026, 4, 22),
        Payer = payer,
        LegalEntity = legal,
        Payment = new Payment(PaymentType.Card, quantity * 500m * 1.21m),
      });
      db.BillItems.Add(new BillItem
      {
        Id = Guid.NewGuid(),
        BillId = id,
        ServiceId = serviceId,
        Quantity = quantity,
        UnitPrice = 500m,
        VatRatePercentage = 21m,
        RecapSingleQuantity = 1u,
        RecapDayQuantity = quantity,
      });
      await db.SaveChangesAsync();
    });
    return (id, serviceId);
  }

  private async Task<Guid> SeedRepairBill()
  {
    (Guid originalId, Guid serviceId) = await SeedOriginalBill(3u);

    var repairId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      Bill? originalBill = await db.Bills.FindAsync(originalId);
      var payer = new Payer
      {
        Name = "John",
        Surname = "Doe",
        Address = new Address(Guid.NewGuid(), "Prague", "10000", "Main", "1"),
      };
      var legal = new LegalEntity
      {
        Name = "Acme",
        Cin = "123",
        Tin = "CZ123",
        Address = new Address(Guid.NewGuid(), "Prague", "10000", "Main", "1"),
      };
      db.Bills.Add(new Bill
      {
        Id = repairId,
        Number = $"IT-{Guid.NewGuid():N}"[..10],
        Kind = BillKind.Repair,
        OriginalBillId = originalId,
        ReservationId = originalBill!.ReservationId,
        LanguageIdGuid = originalBill.LanguageIdGuid,
        IssuedAtUtc = DateTime.UtcNow,
        CheckInAt = originalBill.CheckInAt,
        CheckOutAt = originalBill.CheckOutAt,
        Payer = payer,
        LegalEntity = legal,
        Payment = new Payment(PaymentType.Card, 1m * 500m * 1.21m),
      });
      db.BillItems.Add(new BillItem
      {
        Id = Guid.NewGuid(),
        BillId = repairId,
        ServiceId = serviceId,
        Quantity = 1u,
        UnitPrice = 500m,
        VatRatePercentage = 21m,
        RecapSingleQuantity = 1u,
        RecapDayQuantity = 1u,
      });
      await db.SaveChangesAsync();
    });
    return repairId;
  }

  private static object RepairBody(
    Guid originalId, Guid serviceId, uint quantity,
    decimal unitPrice = 500m, string reason = "test reason") => new
    {
      originalBillId = originalId,
      paymentType = PaymentType.Cash,
      reason,
      items = new[]
    {
      new { serviceId = (Guid?)serviceId, quantity, unitPrice, vatRatePercentage = 21m, recapSingleQuantity = 1u, recapDayQuantity = quantity },
    },
    };

  [Fact]
  public async Task Post_AsAccountant_Returns403()
  {
    (Guid originalId, Guid serviceId) = await SeedOriginalBill();
    HttpClient client = Client(Roles.Accountant);
    HttpResponseMessage response = await client.PostAsJsonAsync("bills/repairs", RepairBody(originalId, serviceId, 1u));
    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Post_HappyPath_Returns201()
  {
    (Guid originalId, Guid serviceId) = await SeedOriginalBill(3u);
    HttpClient client = Client(Roles.Receptionist);
    HttpResponseMessage response = await client.PostAsJsonAsync("bills/repairs", RepairBody(originalId, serviceId, 1u));

    string error = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.Created, error);
  }

  [Fact]
  public async Task Post_CapExceeded_Returns400WithErrorCode()
  {
    (Guid originalId, Guid serviceId) = await SeedOriginalBill(3u);
    HttpClient client = Client(Roles.Receptionist);
    HttpResponseMessage response = await client.PostAsJsonAsync("bills/repairs", RepairBody(originalId, serviceId, 4u));
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Post_OriginalIsRepair_Returns409()
  {
    Guid repairBillId = await SeedRepairBill();
    var serviceId = Guid.NewGuid();
    HttpClient client = Client(Roles.Receptionist);
    HttpResponseMessage response = await client.PostAsJsonAsync("bills/repairs", RepairBody(repairBillId, serviceId, 1u));
    response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
  }

  [Fact]
  public async Task Post_MissingReason_Returns400()
  {
    (Guid originalId, Guid serviceId) = await SeedOriginalBill();
    HttpClient client = Client(Roles.Receptionist);

    var body = new
    {
      originalBillId = originalId,
      paymentType = PaymentType.Cash,
      reason = "",
      items = new[]
      {
        new { serviceId = (Guid?)serviceId, quantity = 1u, unitPrice = 500m, vatRatePercentage = 21m, recapSingleQuantity = 1u, recapDayQuantity = 1u },
      },
    };

    HttpResponseMessage response = await client.PostAsJsonAsync("bills/repairs", body);
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }
}
