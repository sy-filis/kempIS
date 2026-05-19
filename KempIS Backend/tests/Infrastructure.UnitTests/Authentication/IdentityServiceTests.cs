using Application.Abstractions.Authentication;
using Infrastructure.Authentication;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Infrastructure.UnitTests.Authentication;

public sealed class TestIdentityDbContext(DbContextOptions<TestIdentityDbContext> options)
  : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options);

public sealed class IdentityServiceTests : IAsyncLifetime
{
  private SqliteConnection _connection = null!;
  private ServiceProvider _services = null!;
  private UserManager<ApplicationUser> _userManager = null!;
  private IdentityService _service = null!;

  public async Task InitializeAsync()
  {
    _connection = new SqliteConnection("DataSource=:memory:");
    await _connection.OpenAsync();

    ServiceCollection services = new();

    services.AddLogging();

    services.AddDbContext<TestIdentityDbContext>(opts =>
      opts.UseSqlite(_connection));

    services.AddIdentityCore<ApplicationUser>(opts =>
      {
        opts.Password.RequireDigit = false;
        opts.Password.RequiredLength = 1;
        opts.Password.RequireLowercase = false;
        opts.Password.RequireUppercase = false;
        opts.Password.RequireNonAlphanumeric = false;
        // Schema V3 required for GetPasskeysAsync / RemovePasskeyAsync with EF stores.
        opts.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
      })
      .AddRoles<ApplicationRole>()
      .AddEntityFrameworkStores<TestIdentityDbContext>();

    _services = services.BuildServiceProvider();

    TestIdentityDbContext db = _services.GetRequiredService<TestIdentityDbContext>();
    await db.Database.EnsureCreatedAsync();

    RoleManager<ApplicationRole> roleManager = _services.GetRequiredService<RoleManager<ApplicationRole>>();
    foreach (string roleName in Roles.All)
    {
      if (!await roleManager.RoleExistsAsync(roleName))
      {
        await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
      }
    }

    _userManager = _services.GetRequiredService<UserManager<ApplicationUser>>();
    _service = new IdentityService(_userManager);
  }

  public async Task DisposeAsync()
  {
    await _services.DisposeAsync();
    await _connection.DisposeAsync();
  }

  [Fact]
  public async Task CreateUserAsync_NewUser_CreatesUserInRoleWithName()
  {
    Result<Guid> result = await _service.CreateUserAsync("alice", "Alice Walker", Roles.Receptionist, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    ApplicationUser? user = await _userManager.FindByIdAsync(result.Value.ToString());
    user.ShouldNotBeNull();
    user.Name.ShouldBe("Alice Walker");
    IList<string> roles = await _userManager.GetRolesAsync(user);
    roles.ShouldContain(Roles.Receptionist);
  }

  [Fact]
  public async Task CreateUserAsync_UnknownRole_ReturnsInvalidRole()
  {
    Result<Guid> result = await _service.CreateUserAsync("bob", "Bob", "UnknownRole", CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe(AuthErrors.InvalidRole.Code);
  }

  [Fact]
  public async Task CreateUserAsync_DuplicateUsername_ReturnsUsernameTaken()
  {
    Result<Guid> first = await _service.CreateUserAsync("dup-user", "First", Roles.Receptionist, CancellationToken.None);
    first.IsSuccess.ShouldBeTrue();

    Result<Guid> second = await _service.CreateUserAsync("dup-user", "Second", Roles.Manager, CancellationToken.None);

    second.IsFailure.ShouldBeTrue();
    second.Error.Code.ShouldBe(AuthErrors.UsernameTaken.Code);
  }

  [Fact]
  public async Task ListUsersAsync_ReturnsAllActiveUsers()
  {
    await CreateUserInRoleAsync("enabled-user", Roles.Receptionist);
    Guid disabledId = await CreateUserInRoleAsync("disabled-user", Roles.Accountant);
    await _service.DisableUserAsync(disabledId, CancellationToken.None);

    Result<IReadOnlyList<UserSummary>> result = await _service.ListUsersAsync(
      includeDisabled: false,
      role: null,
      cancellationToken: CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldAllBe(u => !u.IsDisabled);
    result.Value.ShouldContain(u => u.Username == "enabled-user");
    result.Value.ShouldNotContain(u => u.Username == "disabled-user");
  }

  [Fact]
  public async Task ListUsersAsync_WithIncludeDisabledTrue_ReturnsBoth()
  {
    await CreateUserInRoleAsync("active", Roles.Receptionist);
    Guid disabledId = await CreateUserInRoleAsync("inactive", Roles.Accountant);
    await _service.DisableUserAsync(disabledId, CancellationToken.None);

    Result<IReadOnlyList<UserSummary>> result = await _service.ListUsersAsync(
      includeDisabled: true,
      role: null,
      cancellationToken: CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldContain(u => u.Username == "active");
    result.Value.ShouldContain(u => u.Username == "inactive");
  }

  [Fact]
  public async Task ListUsersAsync_WithRoleFilter_ReturnsOnlyThatRole()
  {
    await CreateUserInRoleAsync("receptionist-user", Roles.Receptionist);
    await CreateUserInRoleAsync("accountant-user", Roles.Accountant);

    Result<IReadOnlyList<UserSummary>> result = await _service.ListUsersAsync(
      includeDisabled: false,
      role: Roles.Receptionist,
      cancellationToken: CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldContain(u => u.Username == "receptionist-user");
    result.Value.ShouldNotContain(u => u.Username == "accountant-user");
  }

  [Fact]
  public async Task GetUserAsync_ReturnsUserDetailWithPasskeyCount()
  {
    Guid userId = await CreateUserInRoleAsync("detail-user", Roles.Manager);

    Result<UserDetail> result = await _service.GetUserAsync(userId, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Id.ShouldBe(userId);
    result.Value.Username.ShouldBe("detail-user");
    result.Value.PasskeyCount.ShouldBe(0);
    result.Value.Roles.ShouldContain(Roles.Manager);
  }

  [Fact]
  public async Task GetUserAsync_MissingUser_ReturnsUserNotFound()
  {
    var missingId = Guid.NewGuid();

    Result<UserDetail> result = await _service.GetUserAsync(missingId, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Identity.UserNotFound");
  }

  [Fact]
  public async Task DisableUserAsync_SetsLockout()
  {
    Guid userId = await CreateUserInRoleAsync("to-disable", Roles.CleaningStaff);

    Result result = await _service.DisableUserAsync(userId, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    ApplicationUser? user = await _userManager.FindByIdAsync(userId.ToString());
    user.ShouldNotBeNull();
    user.LockoutEnd.ShouldBe(DateTimeOffset.MaxValue);
  }

  [Fact]
  public async Task DisableUserAsync_MissingUser_ReturnsUserNotFound()
  {
    Result result = await _service.DisableUserAsync(Guid.NewGuid(), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Identity.UserNotFound");
  }

  [Fact]
  public async Task UpdateUserAsync_RenamesAndReconcilesRoles()
  {
    Guid userId = await CreateUserInRoleAsync("update-target", Roles.Receptionist);

    Result result = await _service.UpdateUserAsync(
      userId,
      username: "renamed-target",
      name: "Renamed Target",
      roles: [Roles.Manager, Roles.Accountant],
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    ApplicationUser? user = await _userManager.FindByIdAsync(userId.ToString());
    user.ShouldNotBeNull();
    user.UserName.ShouldBe("renamed-target");
    user.Name.ShouldBe("Renamed Target");
    IList<string> roles = await _userManager.GetRolesAsync(user);
    roles.ShouldNotContain(Roles.Receptionist);
    roles.ShouldContain(Roles.Manager);
    roles.ShouldContain(Roles.Accountant);
  }

  [Fact]
  public async Task UpdateUserAsync_UnknownRole_ReturnsRoleInvalid()
  {
    Guid userId = await CreateUserInRoleAsync("update-bogus-role", Roles.Receptionist);

    Result result = await _service.UpdateUserAsync(
      userId,
      username: "update-bogus-role",
      name: "Update Bogus",
      roles: ["BogusRole"],
      CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Identity.RoleInvalid");
  }

  [Fact]
  public async Task UpdateUserAsync_MissingUser_ReturnsUserNotFound()
  {
    Result result = await _service.UpdateUserAsync(
      Guid.NewGuid(),
      username: "ghost",
      name: "Ghost",
      roles: [Roles.Receptionist],
      CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Identity.UserNotFound");
  }

  [Fact]
  public async Task UpdateUserAsync_UsernameTakenByAnotherUser_ReturnsUsernameTaken()
  {
    Guid firstId = await CreateUserInRoleAsync("first-user", Roles.Receptionist);
    await CreateUserInRoleAsync("second-user", Roles.Receptionist);

    Result result = await _service.UpdateUserAsync(
      firstId,
      username: "second-user",
      name: "Tries To Take",
      roles: [Roles.Receptionist],
      CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Auth.UsernameTaken");
  }

  [Fact]
  public async Task ListPasskeysAsync_NewUser_ReturnsEmptyList()
  {
    Guid userId = await CreateUserInRoleAsync("passkey-list-user", Roles.Manager);

    Result<IReadOnlyList<PasskeySummary>> result = await _service.ListPasskeysAsync(userId, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBeEmpty();
  }

  [Fact]
  public async Task ListPasskeysAsync_MissingUser_ReturnsUserNotFound()
  {
    Result<IReadOnlyList<PasskeySummary>> result = await _service.ListPasskeysAsync(Guid.NewGuid(), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Identity.UserNotFound");
  }

  [Fact]
  public async Task RevokePasskeyAsync_MissingUser_ReturnsUserNotFound()
  {
    Result result = await _service.RevokePasskeyAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Identity.UserNotFound");
  }

  [Fact]
  public async Task RevokePasskeyAsync_PasskeyNotFound_ReturnsPasskeyNotFound()
  {
    Guid userId = await CreateUserInRoleAsync("revoke-passkey-user", Roles.Manager);

    Result result = await _service.RevokePasskeyAsync(userId, Guid.NewGuid(), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Identity.PasskeyNotFound");
  }

  private async Task<Guid> CreateUserInRoleAsync(string username, string role)
  {
    ApplicationUser user = new()
    {
      Id = Guid.NewGuid(),
      UserName = username
    };

    IdentityResult createResult = await _userManager.CreateAsync(user);
    createResult.Succeeded.ShouldBeTrue(
      $"CreateAsync failed for '{username}': {string.Join(", ", createResult.Errors.Select(e => e.Description))}");

    IdentityResult roleResult = await _userManager.AddToRoleAsync(user, role);
    roleResult.Succeeded.ShouldBeTrue(
      $"AddToRoleAsync failed for '{username}' -> '{role}': {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");

    return user.Id;
  }
}
