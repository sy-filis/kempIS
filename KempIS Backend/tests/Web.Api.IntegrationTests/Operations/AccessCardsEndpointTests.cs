using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Authentication;
using Application.Abstractions.Gate;
using Application.Operations.AccessCards;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Operations.AccessCards;
using Microsoft.EntityFrameworkCore;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Operations;

public sealed class AccessCardsEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public AccessCardsEndpointTests(ApiFactory factory) => _factory = factory;

  public async Task InitializeAsync()
  {
    await _factory.ResetAllAsync();
    _factory.GateClient.PutCardAsync(default, default!, default).ReturnsForAnyArgs(Task.CompletedTask);
    _factory.GateClient.DeleteCardAsync(default, default).ReturnsForAnyArgs(Task.CompletedTask);
    await _factory.WithDbAsync(async db =>
    {
      db.BillItems.RemoveRange(db.BillItems);
      db.Bills.RemoveRange(db.Bills);
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

  private static Bill BuildMinimalBill(Guid id, string number) =>
    new()
    {
      Id = id,
      Number = number,
      ReservationId = null,
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
      Payment = new Payment(PaymentType.Cash, 0m),
    };

  private async Task<Guid> SeedBillAsync(string number = "B-001")
  {
    var billId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.Bills.Add(BuildMinimalBill(billId, number));
      await db.SaveChangesAsync();
    });
    return billId;
  }

  [Fact]
  public async Task POST_IssueCard_WithBill_PersistsAndReturns201()
  {
    Guid billId = await SeedBillAsync("B-100");
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage response = await client.PostAsJsonAsync(
      new Uri("access-cards", UriKind.Relative),
      new { Uid = 100UL, Deposit = 20m, BillId = billId, ValidUntil = new DateOnly(2026, 8, 15), Note = "VIP" });

    response.StatusCode.ShouldBe(
      HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    AccessCardResponse? body = await response.Content.ReadFromJsonAsync<AccessCardResponse>();
    body.ShouldNotBeNull();
    body.Uid.ShouldBe(100UL);
    body.Deposit.ShouldBe(20m);
    body.Note.ShouldBe("VIP");
    body.Bill.ShouldNotBeNull();
    body.Bill!.Id.ShouldBe(billId);
    body.Bill.Number.ShouldBe("B-100");
    body.IssuedAtUtc.ShouldNotBe(default);
    body.ValidUntil.ShouldBe(new DateOnly(2026, 8, 15));

    await _factory.WithDbAsync(async db =>
    {
      AccessCard? card = await db.AccessCards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == body.Id);
      card.ShouldNotBeNull();
      card.Uid.ShouldBe(100UL);
      card.Deposit.ShouldBe(20m);
      card.BillId.ShouldBe(billId);
      card.Note.ShouldBe("VIP");
    });
  }

  [Fact]
  public async Task POST_IssueCard_WithoutBill_PersistsAndReturns201()
  {
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage response = await client.PostAsJsonAsync(
      new Uri("access-cards", UriKind.Relative),
      new { Uid = 150UL, Deposit = 0m, ValidUntil = new DateOnly(2026, 8, 15), Note = (string?)null });

    response.StatusCode.ShouldBe(
      HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    AccessCardResponse? body = await response.Content.ReadFromJsonAsync<AccessCardResponse>();
    body.ShouldNotBeNull();
    body.Uid.ShouldBe(150UL);
    body.Bill.ShouldBeNull();
    body.Note.ShouldBeNull();

    await _factory.WithDbAsync(async db =>
    {
      AccessCard? card = await db.AccessCards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == body.Id);
      card.ShouldNotBeNull();
      card.BillId.ShouldBeNull();
      card.Note.ShouldBeNull();
    });
  }

  [Fact]
  public async Task POST_IssueCard_DuplicateUid_Returns409()
  {
    Guid billId = await SeedBillAsync("B-200");
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage first = await client.PostAsJsonAsync(
      new Uri("access-cards", UriKind.Relative),
      new { Uid = 200UL, Deposit = 20m, BillId = billId, ValidUntil = new DateOnly(2026, 8, 15) });
    first.StatusCode.ShouldBe(
      HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    HttpResponseMessage second = await client.PostAsJsonAsync(
      new Uri("access-cards", UriKind.Relative),
      new { Uid = 200UL, Deposit = 20m, BillId = billId, ValidUntil = new DateOnly(2026, 8, 15) });

    second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
  }

  [Fact]
  public async Task POST_IssueCard_AfterReturn_ReusesUidSuccessfully()
  {
    Guid billId = await SeedBillAsync("B-300");
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage firstIssue = await client.PostAsJsonAsync(
      new Uri("access-cards", UriKind.Relative),
      new { Uid = 300UL, Deposit = 20m, BillId = billId, ValidUntil = new DateOnly(2026, 8, 15) });
    AccessCardResponse? firstCard = await firstIssue.Content.ReadFromJsonAsync<AccessCardResponse>();
    firstCard.ShouldNotBeNull();

    await _factory.WithDbAsync(async db =>
    {
      AccessCard? existing = await db.AccessCards.FindAsync(firstCard.Id);
      existing.ShouldNotBeNull();
      db.AccessCards.Remove(existing);
      await db.SaveChangesAsync();
    });

    HttpResponseMessage secondIssue = await client.PostAsJsonAsync(
      new Uri("access-cards", UriKind.Relative),
      new { Uid = 300UL, Deposit = 20m, BillId = billId, ValidUntil = new DateOnly(2026, 8, 15) });

    secondIssue.StatusCode.ShouldBe(
      HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
  }

  [Fact]
  public async Task POST_IssueCard_UnknownBill_Returns404()
  {
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage response = await client.PostAsJsonAsync(
      new Uri("access-cards", UriKind.Relative),
      new { Uid = 400UL, Deposit = 20m, BillId = Guid.NewGuid(), ValidUntil = new DateOnly(2026, 8, 15) });

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task GET_All_ReturnsCardsNewestFirst_WithBillSummaryWhenLinked()
  {
    Guid billId = await SeedBillAsync("B-7000");
    HttpClient client = Client(Roles.Receptionist);

    var baseUtc = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
    var linkedId = Guid.NewGuid();
    var unlinkedId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.AccessCards.AddRange(
        new AccessCard { Id = linkedId, Uid = 7001UL, BillId = billId, Deposit = 10m, ValidUntil = new DateOnly(2026, 8, 15), IssuedAtUtc = baseUtc },
        new AccessCard { Id = unlinkedId, Uid = 7002UL, BillId = null, Deposit = 0m, ValidUntil = new DateOnly(2026, 8, 15), IssuedAtUtc = baseUtc.AddMinutes(5), Note = "loaner" });
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await client.GetAsync(new Uri("access-cards", UriKind.Relative));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    AccessCardResponse[]? cards = await response.Content.ReadFromJsonAsync<AccessCardResponse[]>();
    cards.ShouldNotBeNull();
    cards.Length.ShouldBe(2);
    cards[0].Id.ShouldBe(unlinkedId);
    cards[0].Bill.ShouldBeNull();
    cards[0].Note.ShouldBe("loaner");
    cards[1].Id.ShouldBe(linkedId);
    cards[1].Bill.ShouldNotBeNull();
    cards[1].Bill!.Id.ShouldBe(billId);
    cards[1].Bill!.Number.ShouldBe("B-7000");
  }

  [Fact]
  public async Task DELETE_AccessCard_RemovesRow_Returns204()
  {
    Guid billId = await SeedBillAsync("B-5000");
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage issue = await client.PostAsJsonAsync(
      new Uri("access-cards", UriKind.Relative),
      new { Uid = 5000UL, Deposit = 20m, BillId = billId, ValidUntil = new DateOnly(2026, 8, 15) });
    AccessCardResponse? card = await issue.Content.ReadFromJsonAsync<AccessCardResponse>();
    card.ShouldNotBeNull();

    HttpResponseMessage delete = await client.DeleteAsync(
      new Uri($"access-cards/{card.Id}", UriKind.Relative));

    delete.StatusCode.ShouldBe(
      HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    await _factory.WithDbAsync(async db =>
    {
      AccessCard? gone = await db.AccessCards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == card.Id);
      gone.ShouldBeNull();
    });
  }

  [Fact]
  public async Task DELETE_UnknownCard_Returns404()
  {
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage response = await client.DeleteAsync(
      new Uri($"access-cards/{Guid.NewGuid()}", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task POST_Anonymous_Returns401()
  {
    HttpClient client = Client();

    HttpResponseMessage response = await client.PostAsJsonAsync(
      new Uri("access-cards", UriKind.Relative),
      new { Uid = 9001UL, Deposit = 20m, ValidUntil = new DateOnly(2026, 8, 15) });

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task POST_AsCleaner_Returns403()
  {
    HttpClient client = Client(Roles.CleaningStaff);

    HttpResponseMessage response = await client.PostAsJsonAsync(
      new Uri("access-cards", UriKind.Relative),
      new { Uid = 9002UL, Deposit = 20m, ValidUntil = new DateOnly(2026, 8, 15) });

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task GET_Anonymous_Returns401()
  {
    HttpClient client = Client();

    HttpResponseMessage response = await client.GetAsync(new Uri("access-cards", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task DELETE_Anonymous_Returns401()
  {
    HttpClient client = Client();

    HttpResponseMessage response = await client.DeleteAsync(
      new Uri($"access-cards/{Guid.NewGuid()}", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task POST_IssueCard_PushesPutToGate()
  {
    Guid billId = await SeedBillAsync("B-GATE-1");
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage response = await client.PostAsJsonAsync(
      new Uri("access-cards", UriKind.Relative),
      new { Uid = 9100UL, Deposit = 20m, BillId = billId, ValidUntil = new DateOnly(2026, 8, 15), Note = "key" });

    response.StatusCode.ShouldBe(
      HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    await _factory.GateClient.Received(1).PutCardAsync(
      9100UL,
      Arg.Is<GateCardPayload>(p =>
        p.RealName == "John Doe" &&
        p.Note == "key" &&
        p.ValidTo == new DateTimeOffset(2026, 8, 15, 23, 59, 59, TimeSpan.FromHours(2))),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task POST_IssueCard_GateThrows_StillReturns201()
  {
    _factory.GateClient.PutCardAsync(default, default!, default)
      .ThrowsAsyncForAnyArgs(new HttpRequestException("boom"));

    Guid billId = await SeedBillAsync("B-GATE-2");
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage response = await client.PostAsJsonAsync(
      new Uri("access-cards", UriKind.Relative),
      new { Uid = 9200UL, Deposit = 20m, BillId = billId, ValidUntil = new DateOnly(2026, 8, 15) });

    response.StatusCode.ShouldBe(
      HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    await _factory.WithDbAsync(async db =>
      (await db.AccessCards.AsNoTracking().AnyAsync(c => c.Uid == 9200UL)).ShouldBeTrue());
  }

  [Fact]
  public async Task DELETE_AccessCard_PushesDeleteToGate()
  {
    Guid billId = await SeedBillAsync("B-GATE-3");
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage issue = await client.PostAsJsonAsync(
      new Uri("access-cards", UriKind.Relative),
      new { Uid = 9300UL, Deposit = 20m, BillId = billId, ValidUntil = new DateOnly(2026, 8, 15) });
    AccessCardResponse? card = await issue.Content.ReadFromJsonAsync<AccessCardResponse>();
    card.ShouldNotBeNull();

    HttpResponseMessage delete = await client.DeleteAsync(
      new Uri($"access-cards/{card.Id}", UriKind.Relative));

    delete.StatusCode.ShouldBe(
      HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    await _factory.GateClient.Received(1).DeleteCardAsync(9300UL, Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task PATCH_AccessCard_Returns200_UpdatesRowAndPushesGate()
  {
    Guid billId = await SeedBillAsync("B-PATCH-1");
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage issue = await client.PostAsJsonAsync(
      new Uri("access-cards", UriKind.Relative),
      new { Uid = 9500UL, Deposit = 20m, BillId = billId, ValidUntil = new DateOnly(2026, 8, 15), Note = "orig" });
    AccessCardResponse? card = await issue.Content.ReadFromJsonAsync<AccessCardResponse>();
    card.ShouldNotBeNull();

    HttpResponseMessage patch = await client.PatchAsJsonAsync(
      new Uri($"access-cards/{card.Id}", UriKind.Relative),
      new { ValidUntil = new DateOnly(2026, 9, 1), Note = "updated" });

    patch.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    AccessCardResponse? body = await patch.Content.ReadFromJsonAsync<AccessCardResponse>();
    body.ShouldNotBeNull();
    body.Id.ShouldBe(card.Id);
    body.ValidUntil.ShouldBe(new DateOnly(2026, 9, 1));
    body.Note.ShouldBe("updated");
    body.Bill.ShouldNotBeNull();
    body.Bill!.Id.ShouldBe(billId);

    await _factory.WithDbAsync(async db =>
    {
      AccessCard? persisted = await db.AccessCards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == card.Id);
      persisted.ShouldNotBeNull();
      persisted.ValidUntil.ShouldBe(new DateOnly(2026, 9, 1));
      persisted.Note.ShouldBe("updated");
    });

    await _factory.GateClient.Received(2).PutCardAsync(
      9500UL,
      Arg.Any<GateCardPayload>(),
      Arg.Any<CancellationToken>());
    await _factory.GateClient.Received(1).PutCardAsync(
      9500UL,
      Arg.Is<GateCardPayload>(p =>
        p.RealName == "John Doe" &&
        p.Note == "updated" &&
        p.ValidTo == new DateTimeOffset(2026, 9, 1, 23, 59, 59, TimeSpan.FromHours(2))),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task PATCH_AccessCard_GateThrows_StillReturns200()
  {
    Guid billId = await SeedBillAsync("B-PATCH-2");
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage issue = await client.PostAsJsonAsync(
      new Uri("access-cards", UriKind.Relative),
      new { Uid = 9600UL, Deposit = 20m, BillId = billId, ValidUntil = new DateOnly(2026, 8, 15) });
    AccessCardResponse? card = await issue.Content.ReadFromJsonAsync<AccessCardResponse>();
    card.ShouldNotBeNull();

    _factory.GateClient.PutCardAsync(default, default!, default)
      .ThrowsAsyncForAnyArgs(new HttpRequestException("boom"));

    HttpResponseMessage patch = await client.PatchAsJsonAsync(
      new Uri($"access-cards/{card.Id}", UriKind.Relative),
      new { ValidUntil = new DateOnly(2026, 9, 2), Note = "post-throw" });

    patch.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    await _factory.WithDbAsync(async db =>
    {
      AccessCard? persisted = await db.AccessCards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == card.Id);
      persisted.ShouldNotBeNull();
      persisted.ValidUntil.ShouldBe(new DateOnly(2026, 9, 2));
      persisted.Note.ShouldBe("post-throw");
    });
  }

  [Fact]
  public async Task PATCH_UnknownCard_Returns404()
  {
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage response = await client.PatchAsJsonAsync(
      new Uri($"access-cards/{Guid.NewGuid()}", UriKind.Relative),
      new { ValidUntil = new DateOnly(2026, 9, 2), Note = (string?)null });

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task PATCH_Anonymous_Returns401()
  {
    HttpClient client = Client();

    HttpResponseMessage response = await client.PatchAsJsonAsync(
      new Uri($"access-cards/{Guid.NewGuid()}", UriKind.Relative),
      new { ValidUntil = new DateOnly(2026, 9, 2), Note = (string?)null });

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task DELETE_AccessCard_GateThrows_StillReturns204()
  {
    Guid billId = await SeedBillAsync("B-GATE-4");
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage issue = await client.PostAsJsonAsync(
      new Uri("access-cards", UriKind.Relative),
      new { Uid = 9400UL, Deposit = 20m, BillId = billId, ValidUntil = new DateOnly(2026, 8, 15) });
    AccessCardResponse? card = await issue.Content.ReadFromJsonAsync<AccessCardResponse>();
    card.ShouldNotBeNull();

    _factory.GateClient.DeleteCardAsync(default, default)
      .ThrowsAsyncForAnyArgs(new HttpRequestException("boom"));

    HttpResponseMessage delete = await client.DeleteAsync(
      new Uri($"access-cards/{card.Id}", UriKind.Relative));

    delete.StatusCode.ShouldBe(
      HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    await _factory.WithDbAsync(async db =>
      (await db.AccessCards.AsNoTracking().AnyAsync(c => c.Id == card.Id)).ShouldBeFalse());
  }
}
