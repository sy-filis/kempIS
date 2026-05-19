using Application.Abstractions.Authentication;
using Application.Operations.MaintenanceIssues;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Operations;

public sealed class MaintenanceIssuesEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public MaintenanceIssuesEndpointTests(ApiFactory factory) => _factory = factory;

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
  public async Task Create_WithoutSpotId_IsAllowed()
  {
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage response = await client.PostAsJsonAsync(
      new Uri("maintenance-issues", UriKind.Relative),
      new { ProblemDescription = "Water leak in cabin 5" });

    response.StatusCode.ShouldBe(
      HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    Guid newId = await response.Content.ReadFromJsonAsync<Guid>();
    newId.ShouldNotBe(Guid.Empty);
  }

  [Fact]
  public async Task Resolve_StampsResolvedAtUtc()
  {
    HttpClient receptionistClient = Client(Roles.Receptionist);

    HttpResponseMessage createResponse = await receptionistClient.PostAsJsonAsync(
      new Uri("maintenance-issues", UriKind.Relative),
      new { ProblemDescription = "Broken window" });

    createResponse.StatusCode.ShouldBe(
      HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    Guid issueId = await createResponse.Content.ReadFromJsonAsync<Guid>();

    HttpClient staffClient = Client(Roles.CleaningStaff);
    HttpResponseMessage resolveResponse = await staffClient.PostAsJsonAsync(
      new Uri($"maintenance-issues/{issueId}/resolve", UriKind.Relative),
      new { });

    resolveResponse.StatusCode.ShouldBe(
      HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex2) ? ex2.ToString() : "no exception");

    await _factory.WithDbAsync(async db =>
    {
      Domain.Operations.MaintenanceIssues.MaintenanceIssue? issue =
        await db.MaintenanceIssues.FindAsync(issueId);
      issue.ShouldNotBeNull();
      issue.ResolvedAtUtc.ShouldNotBeNull();
    });
  }

  [Fact]
  public async Task Resolve_WhenAlreadyResolved_Returns409()
  {
    HttpClient receptionistClient = Client(Roles.Receptionist);

    HttpResponseMessage createResponse = await receptionistClient.PostAsJsonAsync(
      new Uri("maintenance-issues", UriKind.Relative),
      new { ProblemDescription = "Faulty socket" });

    createResponse.StatusCode.ShouldBe(
      HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    Guid issueId = await createResponse.Content.ReadFromJsonAsync<Guid>();

    HttpClient staffClient = Client(Roles.CleaningStaff);

    HttpResponseMessage first = await staffClient.PostAsJsonAsync(
      new Uri($"maintenance-issues/{issueId}/resolve", UriKind.Relative),
      new { });
    first.StatusCode.ShouldBe(
      HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex2) ? ex2.ToString() : "no exception");

    HttpResponseMessage second = await staffClient.PostAsJsonAsync(
      new Uri($"maintenance-issues/{issueId}/resolve", UriKind.Relative),
      new { });
    second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
  }

  [Fact]
  public async Task Delete_AsReceptionist_Returns403()
  {
    HttpClient receptionistClient = Client(Roles.Receptionist);

    HttpResponseMessage createResponse = await receptionistClient.PostAsJsonAsync(
      new Uri("maintenance-issues", UriKind.Relative),
      new { ProblemDescription = "Door hinge broken" });

    createResponse.StatusCode.ShouldBe(
      HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    Guid issueId = await createResponse.Content.ReadFromJsonAsync<Guid>();

    HttpResponseMessage deleteResponse = await receptionistClient.DeleteAsync(
      new Uri($"maintenance-issues/{issueId}", UriKind.Relative));

    deleteResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task List_WithStatusOpen_FiltersResolvedOut()
  {
    HttpClient receptionistClient = Client(Roles.Receptionist);

    HttpResponseMessage create1 = await receptionistClient.PostAsJsonAsync(
      new Uri("maintenance-issues", UriKind.Relative),
      new { ProblemDescription = "Open issue" });
    create1.StatusCode.ShouldBe(
      HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
    Guid openId = await create1.Content.ReadFromJsonAsync<Guid>();

    HttpResponseMessage create2 = await receptionistClient.PostAsJsonAsync(
      new Uri("maintenance-issues", UriKind.Relative),
      new { ProblemDescription = "Resolved issue" });
    create2.StatusCode.ShouldBe(
      HttpStatusCode.Created,
      _factory.ServerExceptions.TryPeek(out Exception? ex2) ? ex2.ToString() : "no exception");
    Guid resolvedId = await create2.Content.ReadFromJsonAsync<Guid>();

    HttpClient staffClient = Client(Roles.CleaningStaff);
    HttpResponseMessage resolveResponse = await staffClient.PostAsJsonAsync(
      new Uri($"maintenance-issues/{resolvedId}/resolve", UriKind.Relative),
      new { });
    resolveResponse.StatusCode.ShouldBe(
      HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex3) ? ex3.ToString() : "no exception");

    HttpResponseMessage listResponse = await Client(Roles.Receptionist).GetAsync(
      new Uri("maintenance-issues?status=Open", UriKind.Relative));
    listResponse.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex4) ? ex4.ToString() : "no exception");

    List<MaintenanceIssueResponse>? issues =
      await listResponse.Content.ReadFromJsonAsync<List<MaintenanceIssueResponse>>();
    issues.ShouldNotBeNull();
    issues.ShouldContain(i => i.Id == openId);
    issues.ShouldNotContain(i => i.Id == resolvedId);
  }
}
