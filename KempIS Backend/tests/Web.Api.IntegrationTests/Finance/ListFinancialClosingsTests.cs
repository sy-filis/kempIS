using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Authentication;
using Application.Finance.FinancialClosings.ListFinancialClosings;
using Domain.Finance.FinancialClosings;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Finance;

public sealed class ListFinancialClosingsTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public ListFinancialClosingsTests(ApiFactory factory) => _factory = factory;

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
  public async Task List_ReturnsAllClosings_ForAccountant()
  {
    var closing1Id = Guid.NewGuid();
    var closing2Id = Guid.NewGuid();
    var closing1At = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
    var closing2At = new DateTime(2026, 4, 10, 10, 0, 0, DateTimeKind.Utc);
    var creator1 = Guid.NewGuid();
    var creator2 = Guid.NewGuid();

    await _factory.WithDbAsync(async db =>
    {
      db.FinancialClosings.Add(new FinancialClosing
      {
        Id = closing1Id,
        FinancialClosingId = 1u,
        ClosedAtUtc = closing1At,
        TotalAmount = 500m,
        CreatedByUserId = creator1,
      });
      db.FinancialClosings.Add(new FinancialClosing
      {
        Id = closing2Id,
        FinancialClosingId = 2u,
        ClosedAtUtc = closing2At,
        TotalAmount = 1200m,
        CreatedByUserId = creator2,
      });
      await db.SaveChangesAsync();
    });

    HttpClient client = Client(Roles.Accountant);
    HttpResponseMessage response = await client.GetAsync("financial-closings");

    string errorContext = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.OK, errorContext);

    List<FinancialClosingSummary>? body =
      await response.Content.ReadFromJsonAsync<List<FinancialClosingSummary>>();
    body.ShouldNotBeNull();
    body!.Count.ShouldBe(2, errorContext);

    body[0].Id.ShouldBe(closing2Id);
    body[0].FinancialClosingId.ShouldBe(2u);
    body[0].TotalAmount.ShouldBe(1200m);
    body[0].BillCount.ShouldBe(0);
    body[0].CreatedByUserId.ShouldBe(creator2);

    body[1].Id.ShouldBe(closing1Id);
    body[1].FinancialClosingId.ShouldBe(1u);
    body[1].TotalAmount.ShouldBe(500m);
    body[1].CreatedByUserId.ShouldBe(creator1);
  }

  [Fact]
  public async Task List_FiltersByDateRange()
  {
    var earlierId = Guid.NewGuid();
    var laterId = Guid.NewGuid();
    var earlierAt = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
    var laterAt = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    await _factory.WithDbAsync(async db =>
    {
      db.FinancialClosings.Add(new FinancialClosing
      {
        Id = earlierId,
        FinancialClosingId = 1u,
        ClosedAtUtc = earlierAt,
        TotalAmount = 300m,
      });
      db.FinancialClosings.Add(new FinancialClosing
      {
        Id = laterId,
        FinancialClosingId = 2u,
        ClosedAtUtc = laterAt,
        TotalAmount = 800m,
      });
      await db.SaveChangesAsync();
    });

    HttpClient client = Client(Roles.Receptionist);
    HttpResponseMessage response = await client.GetAsync("financial-closings?from=2026-04-01");

    string errorContext = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    response.StatusCode.ShouldBe(HttpStatusCode.OK, errorContext);

    List<FinancialClosingSummary>? body =
      await response.Content.ReadFromJsonAsync<List<FinancialClosingSummary>>();
    body.ShouldNotBeNull();
    body!.Count.ShouldBe(1, errorContext);
    body[0].Id.ShouldBe(laterId);
    body[0].FinancialClosingId.ShouldBe(2u);
  }
}
