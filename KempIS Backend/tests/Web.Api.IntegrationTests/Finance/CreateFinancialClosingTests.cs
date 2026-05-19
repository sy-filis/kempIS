using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Authentication;
using Application.Finance.FinancialClosings;
using Application.Finance.FinancialClosings.CreateFinancialClosing;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using SharedKernel;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Finance;

public sealed class CreateFinancialClosingTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public CreateFinancialClosingTests(ApiFactory factory) => _factory = factory;

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

    _factory.FinancialClosingReportRenderer
      .RenderAsync(default!, default)
      .ReturnsForAnyArgs(Result.Success(new byte[] { 0x25, 0x50, 0x44, 0x46 }));
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

  private static Bill CreateMinimalBill(Guid id, string number, decimal paymentAmount) =>
    new()
    {
      Id = id,
      Number = number,
      ReservationId = Guid.NewGuid(),
      IssuedAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc),
      CheckInAt = new DateOnly(2026, 4, 19),
      CheckOutAt = new DateOnly(2026, 4, 21),
      LanguageIdGuid = Guid.NewGuid(),
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
      Payment = new Payment(PaymentType.Cash, paymentAmount),
    };

  [Fact]
  public async Task Create_SweepsAllOpenBills_AssignsSequentialId()
  {
    var bill1Id = Guid.NewGuid();
    var bill2Id = Guid.NewGuid();
    var callerId = Guid.NewGuid();

    await _factory.WithDbAsync(async db =>
    {
      db.Bills.Add(CreateMinimalBill(bill1Id, "B-001", 300m));
      db.Bills.Add(CreateMinimalBill(bill2Id, "B-002", 700m));
      await db.SaveChangesAsync();
    });

    HttpClient client = Client(Roles.Receptionist);
    client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, callerId.ToString());
    HttpResponseMessage response = await client.PostAsync("financial-closings", null);

    string errorContext = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.Created, errorContext);

    CreateFinancialClosingResponse? body =
      await response.Content.ReadFromJsonAsync<CreateFinancialClosingResponse>();
    body.ShouldNotBeNull();
    body!.FinancialClosingId.ShouldBe(1u);
    body.TotalAmount.ShouldBe(1000m);
    body.BillCount.ShouldBe(2);
    body.Id.ShouldNotBe(Guid.Empty);

    await _factory.WithDbAsync(async db =>
    {
      bool anyOpen = await db.Bills.AnyAsync(b => b.FinancialClosingId == null);
      anyOpen.ShouldBeFalse("all bills should be swept into the closing");

      Guid? persistedCreator = await db.FinancialClosings
        .Where(c => c.Id == body.Id)
        .Select(c => c.CreatedByUserId)
        .SingleAsync();
      persistedCreator.ShouldBe(callerId);
    });
  }

  [Fact]
  public async Task Create_WithNoOpenBills_Returns409()
  {
    HttpClient client = Client(Roles.Accountant);
    HttpResponseMessage response = await client.PostAsync("financial-closings", null);

    response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
  }

  [Fact]
  public async Task Create_AsCleaningStaff_Returns403()
  {
    HttpClient client = Client(Roles.CleaningStaff);
    HttpResponseMessage response = await client.PostAsync("financial-closings", null);

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }
}
