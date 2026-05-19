using Application.Abstractions.Authentication;
using Application.Abstractions.EDoklady;
using SharedKernel;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.EDoklady;

public sealed class EDokladyEndpointsTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public EDokladyEndpointsTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync()
  {
    _factory.EDokladyClient.ClearReceivedCalls();
    return Task.CompletedTask;
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

  private static VirtualServiceCounter SampleCounter(string id = "vsc-1") =>
      new(id, "Recepce",
          new QrCode("QR-DATA", new DateTime(2026, 5, 21, 12, 0, 0, DateTimeKind.Utc)),
          new Geolocation(50.08, 14.42, 20));

  [Fact]
  public async Task GetVirtualServiceCounter_Anonymous_Returns401()
  {
    HttpResponseMessage r = await Client().GetAsync(new Uri("edoklady/virtual-service-counters/vsc-1", UriKind.Relative));
    r.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GetVirtualServiceCounter_AsAccountant_Returns403()
  {
    HttpResponseMessage r = await Client(Roles.Accountant).GetAsync(
        new Uri("edoklady/virtual-service-counters/vsc-1", UriKind.Relative));
    r.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task CreateVirtualServiceCounter_HappyPath_Returns201AndBody()
  {
    _factory.EDokladyClient
      .CreateVirtualServiceCounterAsync(Arg.Any<CreateVirtualServiceCounterRequest>(), Arg.Any<CancellationToken>())
      .Returns(Result.Success(SampleCounter()));

    HttpResponseMessage r = await Client(Roles.Receptionist).PostAsJsonAsync(
        new Uri("edoklady/virtual-service-counters", UriKind.Relative),
        new CreateVirtualServiceCounterRequest("Recepce"));

    r.StatusCode.ShouldBe(HttpStatusCode.Created,
        _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    VirtualServiceCounter? body = await r.Content.ReadFromJsonAsync<VirtualServiceCounter>();
    body.ShouldNotBeNull();
    body.Id.ShouldBe("vsc-1");
    body.QrCode.Data.ShouldBe("QR-DATA");
  }

  [Fact]
  public async Task GetVirtualServiceCounter_HappyPath_Returns200()
  {
    _factory.EDokladyClient
      .GetVirtualServiceCounterAsync("vsc-1", Arg.Any<CancellationToken>())
      .Returns(Result.Success(SampleCounter()));

    HttpResponseMessage r = await Client(Roles.Receptionist).GetAsync(
        new Uri("edoklady/virtual-service-counters/vsc-1", UriKind.Relative));

    r.StatusCode.ShouldBe(HttpStatusCode.OK);
    VirtualServiceCounter? body = await r.Content.ReadFromJsonAsync<VirtualServiceCounter>();
    body.ShouldNotBeNull();
    body.Id.ShouldBe("vsc-1");
  }

  [Fact]
  public async Task GetVirtualServiceCounter_ClientReturnsNotFound_Returns404()
  {
    _factory.EDokladyClient
      .GetVirtualServiceCounterAsync("missing", Arg.Any<CancellationToken>())
      .Returns(Result.Failure<VirtualServiceCounter>(EDokladyErrors.NotFound("missing")));

    HttpResponseMessage r = await Client(Roles.Receptionist).GetAsync(
        new Uri("edoklady/virtual-service-counters/missing", UriKind.Relative));

    r.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task GetVirtualServiceCounter_ClientReturnsUnavailable_Returns500()
  {
    _factory.EDokladyClient
      .GetVirtualServiceCounterAsync("vsc-1", Arg.Any<CancellationToken>())
      .Returns(Result.Failure<VirtualServiceCounter>(EDokladyErrors.Unavailable));

    HttpResponseMessage r = await Client(Roles.Receptionist).GetAsync(
        new Uri("edoklady/virtual-service-counters/vsc-1", UriKind.Relative));

    r.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
  }

  [Fact]
  public async Task StartPresentation_HappyPath_Returns201AndTransactionId()
  {
    _factory.EDokladyClient
      .StartPresentationAsync("vsc-1", Arg.Any<CancellationToken>())
      .Returns(Result.Success("tx-42"));

    HttpResponseMessage r = await Client(Roles.Receptionist).PostAsJsonAsync(
        new Uri("edoklady/presentations", UriKind.Relative),
        new { virtualServiceCounterId = "vsc-1" });

    r.StatusCode.ShouldBe(HttpStatusCode.Created);
    StartPresentationResponseBody? body = await r.Content.ReadFromJsonAsync<StartPresentationResponseBody>();
    body.ShouldNotBeNull();
    body.TransactionId.ShouldBe("tx-42");
  }

  [Fact]
  public async Task StartPresentation_EmptyVirtualServiceCounterId_Returns400()
  {
    HttpResponseMessage r = await Client(Roles.Receptionist).PostAsJsonAsync(
        new Uri("edoklady/presentations", UriKind.Relative),
        new { virtualServiceCounterId = "" });

    r.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    await _factory.EDokladyClient.DidNotReceive()
      .StartPresentationAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task GetTransaction_HappyPath_Returns200()
  {
    _factory.EDokladyClient
      .GetTransactionAsync("tx-42", Arg.Any<CancellationToken>())
      .Returns(Result.Success(new TransactionState(
          "tx-42",
          TransactionStateKind.WaitingForResponse,
          new DateTime(2026, 4, 21, 13, 0, 0, DateTimeKind.Utc))));

    HttpResponseMessage r = await Client(Roles.Receptionist).GetAsync(
        new Uri("edoklady/presentations/tx-42", UriKind.Relative));

    r.StatusCode.ShouldBe(HttpStatusCode.OK);
    TransactionState? body = await r.Content.ReadFromJsonAsync<TransactionState>();
    body.ShouldNotBeNull();
    body.State.ShouldBe(TransactionStateKind.WaitingForResponse);
  }

  [Fact]
  public async Task GetTransactionResult_HappyPath_Returns200_AndPassesFlags()
  {
    _factory.EDokladyClient
      .GetTransactionResultAsync("tx-42", true, true, Arg.Any<CancellationToken>())
      .Returns(Result.Success(new TransactionResult(
          PresentationOutcome.Success,
          [new PresentedDocument(
              "org.iso.18013.5.1.CZ.mID",
              [new PresentedAttribute("given_name", AttributeDataType.String, "Jan")],
              Missing: null,
              MDoc: null)])));

    HttpResponseMessage r = await Client(Roles.Receptionist).GetAsync(
        new Uri("edoklady/presentations/tx-42/result?includeMDoc=true&includeMissingCredentials=true", UriKind.Relative));

    r.StatusCode.ShouldBe(HttpStatusCode.OK);
    TransactionResult? body = await r.Content.ReadFromJsonAsync<TransactionResult>();
    body.ShouldNotBeNull();
    body.Outcome.ShouldBe(PresentationOutcome.Success);
    body.Documents[0].Obtained[0].Value.ShouldBe("Jan");
  }

  [Fact]
  public async Task GetTransactionResult_DefaultQueryFlags_AreFalse()
  {
    _factory.EDokladyClient
      .GetTransactionResultAsync("tx-42", false, false, Arg.Any<CancellationToken>())
      .Returns(Result.Success(new TransactionResult(PresentationOutcome.Success, [])));

    HttpResponseMessage r = await Client(Roles.Receptionist).GetAsync(
        new Uri("edoklady/presentations/tx-42/result", UriKind.Relative));

    r.StatusCode.ShouldBe(HttpStatusCode.OK);
    await _factory.EDokladyClient.Received(1).GetTransactionResultAsync(
        "tx-42", false, false, Arg.Any<CancellationToken>());
  }

  private sealed record StartPresentationResponseBody(string TransactionId);
}
