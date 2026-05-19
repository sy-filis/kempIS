using Application.Abstractions.Authentication;
using SharedKernel;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Identity;

public sealed class UsersEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public UsersEndpointTests(ApiFactory factory) => _factory = factory;

  private static readonly string[] UnknownRoleSet = ["Nonsense"];
  private static readonly string[] ReceptionistAndAccountant = [Roles.Receptionist, Roles.Accountant];
  private static readonly string[] ReceptionistOnly = [Roles.Receptionist];

  public async Task InitializeAsync()
  {
    await _factory.ResetAllAsync();
    _factory.IdentityService.Reset();
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
  public async Task List_AsManager_ReturnsUsers()
  {
    var userId = Guid.NewGuid();
    IReadOnlyList<UserSummary> users =
    [
      new UserSummary(userId, "testuser", "Test User", [Roles.Receptionist], false, DateTime.UtcNow),
    ];
    _factory.IdentityService.ListUsersAsyncResult = Result.Success(users);

    HttpResponseMessage response = await Client(Roles.Manager)
      .GetAsync(new Uri("users", UriKind.Relative));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    IReadOnlyList<UserSummary>? result =
      await response.Content.ReadFromJsonAsync<IReadOnlyList<UserSummary>>();
    result.ShouldNotBeNull();
    result.Count.ShouldBeGreaterThanOrEqualTo(1);
  }

  [Fact]
  public async Task List_AsReceptionist_ReturnsUsers()
  {
    var userId = Guid.NewGuid();
    IReadOnlyList<UserSummary> users =
    [
      new UserSummary(userId, "testuser", "Test User", [Roles.Receptionist], false, DateTime.UtcNow),
    ];
    _factory.IdentityService.ListUsersAsyncResult = Result.Success(users);

    HttpResponseMessage response = await Client(Roles.Receptionist)
      .GetAsync(new Uri("users", UriKind.Relative));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
  }

  [Fact]
  public async Task UpdateUser_UnknownRole_Returns400()
  {
    var userId = Guid.NewGuid();

    HttpResponseMessage response = await Client(Roles.Manager)
      .PutAsJsonAsync(new Uri($"users/{userId}", UriKind.Relative), new
      {
        Username = "renamed-user",
        Name = "Renamed User",
        Roles = UnknownRoleSet,
      });

    response.StatusCode.ShouldBe(
      HttpStatusCode.BadRequest,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
  }

  [Fact]
  public async Task UpdateUser_ValidPayload_Returns204AndForwardsRoles()
  {
    var userId = Guid.NewGuid();
    _factory.IdentityService.UpdateUserAsyncResult = Result.Success();

    HttpResponseMessage response = await Client(Roles.Manager)
      .PutAsJsonAsync(new Uri($"users/{userId}", UriKind.Relative), new
      {
        Username = "renamed-user",
        Name = "Renamed User",
        Roles = ReceptionistAndAccountant,
      });

    response.StatusCode.ShouldBe(
      HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
    _factory.IdentityService.LastUsername.ShouldBe("renamed-user");
    _factory.IdentityService.LastName.ShouldBe("Renamed User");
    _factory.IdentityService.LastRoles.ShouldNotBeNull();
    _factory.IdentityService.LastRoles!.ShouldBe(ReceptionistAndAccountant);
  }

  [Fact]
  public async Task UpdateUser_EmptyRoles_Returns400()
  {
    var userId = Guid.NewGuid();

    HttpResponseMessage response = await Client(Roles.Manager)
      .PutAsJsonAsync(new Uri($"users/{userId}", UriKind.Relative), new
      {
        Username = "renamed-user",
        Name = "Renamed User",
        Roles = Array.Empty<string>(),
      });

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task UpdateUser_AsReceptionist_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist)
      .PutAsJsonAsync(new Uri($"users/{Guid.NewGuid()}", UriKind.Relative), new
      {
        Username = "renamed-user",
        Name = "Renamed User",
        Roles = ReceptionistOnly,
      });

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task RevokePasskey_AsReceptionist_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist)
      .DeleteAsync(new Uri($"users/{Guid.NewGuid()}/passkeys/{Guid.NewGuid()}", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }
}
