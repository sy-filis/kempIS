using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Authentication;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Finance;

public sealed class InvoiceLifecycleEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public InvoiceLifecycleEndpointTests(ApiFactory factory) => _factory = factory;

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

  private static object CreatePayload() => new
  {
    reservationId = Guid.NewGuid(),
    payer = new { name = "John", surname = "Doe", address = Addr() },
    legalEntity = (object?)null,
    email = "billing@example.com",
    phoneNumber = "+420123456789",
    items = new[] { new { serviceGuid = Guid.NewGuid(), quantity = 2m, unitPrice = 500m, vatRatePercentage = 21m } },
  };

  private static object UpdatePayload() => new
  {
    payer = (object?)null,
    legalEntity = new { name = "Acme", cin = "123", tin = "CZ123", address = Addr() },
    email = "billing@example.com",
    phoneNumber = "+420123456789",
    items = new[] { new { serviceGuid = Guid.NewGuid(), quantity = 3m, unitPrice = 600m, vatRatePercentage = 21m } },
  };

  private static object Addr() => new { countryId = Guid.NewGuid(), city = "Prague", zipCode = "10000", street = "Main", houseNumber = "1" };

  [Fact]
  public async Task FullLifecycle_DraftCreatedPaid()
  {
    HttpClient receptionist = Client(Roles.Receptionist);
    HttpClient accountant = Client(Roles.Accountant);

    HttpResponseMessage createResponse = await receptionist.PostAsJsonAsync("invoices", CreatePayload());
    string err = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    createResponse.StatusCode.ShouldBe(HttpStatusCode.Created, err);

    Dictionary<string, Guid>? body = await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
    Guid invoiceId = body!["invoiceId"];

    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    DateOnly due = today.AddDays(14);

    HttpResponseMessage recMarkCreated = await receptionist.PostAsJsonAsync(
      $"invoices/{invoiceId}/mark-created", new { number = "EXT-1", issuedAt = today, dueTo = due });
    recMarkCreated.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

    HttpResponseMessage markCreated = await accountant.PostAsJsonAsync(
      $"invoices/{invoiceId}/mark-created", new { number = "EXT-1", issuedAt = today, dueTo = due });
    err = _factory.ServerExceptions.TryPeek(out ex) ? ex.ToString() : "no exception";
    markCreated.StatusCode.ShouldBe(HttpStatusCode.NoContent, err);

    HttpResponseMessage update = await receptionist.PutAsJsonAsync($"invoices/{invoiceId}", UpdatePayload());
    update.StatusCode.ShouldBe(HttpStatusCode.Conflict);

    HttpResponseMessage markPaid = await accountant.PostAsJsonAsync(
      $"invoices/{invoiceId}/mark-paid", new { paidAt = today });
    err = _factory.ServerExceptions.TryPeek(out ex) ? ex.ToString() : "no exception";
    markPaid.StatusCode.ShouldBe(HttpStatusCode.NoContent, err);
  }

  [Fact]
  public async Task Update_AsReceptionist_OnDraft_Returns204()
  {
    HttpClient receptionist = Client(Roles.Receptionist);
    HttpResponseMessage createResponse = await receptionist.PostAsJsonAsync("invoices", CreatePayload());
    Dictionary<string, Guid> body = (await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>())!;
    Guid invoiceId = body["invoiceId"];

    HttpResponseMessage update = await receptionist.PutAsJsonAsync($"invoices/{invoiceId}", UpdatePayload());
    string err = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    update.StatusCode.ShouldBe(HttpStatusCode.NoContent, err);
  }

  [Fact]
  public async Task Delete_AsReceptionist_OnDraft_Returns204()
  {
    HttpClient receptionist = Client(Roles.Receptionist);
    HttpResponseMessage createResponse = await receptionist.PostAsJsonAsync("invoices", CreatePayload());
    Dictionary<string, Guid> body = (await createResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>())!;
    Guid invoiceId = body["invoiceId"];

    HttpResponseMessage delete = await receptionist.DeleteAsync($"invoices/{invoiceId}");
    string err = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";
    delete.StatusCode.ShouldBe(HttpStatusCode.NoContent, err);
  }

  [Fact]
  public async Task Create_WithLegalEntityOnly_Returns201()
  {
    HttpClient receptionist = Client(Roles.Receptionist);
    var payload = new
    {
      reservationId = Guid.NewGuid(),
      payer = (object?)null,
      legalEntity = new { name = "Acme", cin = "123", tin = "CZ123", address = Addr() },
      email = "billing@example.com",
      phoneNumber = "+420123456789",
      items = new[] { new { serviceGuid = Guid.NewGuid(), quantity = 2m, unitPrice = 500m, vatRatePercentage = 21m } },
    };

    HttpResponseMessage response = await receptionist.PostAsJsonAsync("invoices", payload);
    string err = _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception";

    response.StatusCode.ShouldBe(HttpStatusCode.Created, err);
  }

  [Fact]
  public async Task Create_WithBothPayerAndLegalEntity_Returns400()
  {
    HttpClient receptionist = Client(Roles.Receptionist);
    var payload = new
    {
      reservationId = Guid.NewGuid(),
      payer = new { name = "John", surname = "Doe", address = Addr() },
      legalEntity = new { name = "Acme", cin = "123", tin = "CZ123", address = Addr() },
      email = "billing@example.com",
      phoneNumber = "+420123456789",
      items = new[] { new { serviceGuid = Guid.NewGuid(), quantity = 2m, unitPrice = 500m, vatRatePercentage = 21m } },
    };

    HttpResponseMessage response = await receptionist.PostAsJsonAsync("invoices", payload);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Create_WithNeitherPayerNorLegalEntity_Returns400()
  {
    HttpClient receptionist = Client(Roles.Receptionist);
    var payload = new
    {
      reservationId = Guid.NewGuid(),
      payer = (object?)null,
      legalEntity = (object?)null,
      email = "billing@example.com",
      phoneNumber = "+420123456789",
      items = new[] { new { serviceGuid = Guid.NewGuid(), quantity = 2m, unitPrice = 500m, vatRatePercentage = 21m } },
    };

    HttpResponseMessage response = await receptionist.PostAsJsonAsync("invoices", payload);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Create_WithMalformedEmail_Returns400()
  {
    HttpClient receptionist = Client(Roles.Receptionist);
    var payload = new
    {
      reservationId = Guid.NewGuid(),
      payer = new { name = "John", surname = "Doe", address = Addr() },
      legalEntity = (object?)null,
      email = "not-an-email",
      phoneNumber = "+420123456789",
      items = new[] { new { serviceGuid = Guid.NewGuid(), quantity = 2m, unitPrice = 500m, vatRatePercentage = 21m } },
    };

    HttpResponseMessage response = await receptionist.PostAsJsonAsync("invoices", payload);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Create_WithEmptyPhoneNumber_Returns400()
  {
    HttpClient receptionist = Client(Roles.Receptionist);
    var payload = new
    {
      reservationId = Guid.NewGuid(),
      payer = new { name = "John", surname = "Doe", address = Addr() },
      legalEntity = (object?)null,
      email = "billing@example.com",
      phoneNumber = string.Empty,
      items = new[] { new { serviceGuid = Guid.NewGuid(), quantity = 2m, unitPrice = 500m, vatRatePercentage = 21m } },
    };

    HttpResponseMessage response = await receptionist.PostAsJsonAsync("invoices", payload);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }
}
