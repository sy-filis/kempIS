using Application.Abstractions.Authentication;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Auth;

public sealed class AuthLifecycleTests : IClassFixture<AuthLifecycleApiFactory>
{
  private readonly AuthLifecycleApiFactory _factory;
  private readonly HttpClient _client;

  public AuthLifecycleTests(AuthLifecycleApiFactory factory)
  {
    _factory = factory;
    _client = factory.CreateClient();
  }

  [Fact]
  public async Task Login_HappyPath_ReturnsWellFormedAccessTokenResponse()
  {
    Guid userId = await SeedUserAsync(username: "login-happy-user");

    _factory.PasskeyAuthenticator.NextAssertion =
      Result.Success(new PasskeyAssertionOutcome(userId));

    HttpResponseMessage response = await _client.PostAsJsonAsync(
      new Uri("auth/passkeys/login/verify", UriKind.Relative),
      new { Credential = "credential-payload" });

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    AccessTokenResponseDto? body = await response.Content.ReadFromJsonAsync<AccessTokenResponseDto>();
    body.ShouldNotBeNull();
    body.TokenType.ShouldBe("Bearer");
    body.AccessToken.ShouldNotBeNullOrWhiteSpace();
    body.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    body.ExpiresIn.ShouldBeGreaterThan(0);
  }

  [Fact]
  public async Task Refresh_HappyPath_ReturnsNewPair()
  {
    Guid userId = await SeedUserAsync(username: "refresh-happy-user");

    _factory.PasskeyAuthenticator.NextAssertion =
      Result.Success(new PasskeyAssertionOutcome(userId));
    HttpResponseMessage loginResponse = await _client.PostAsJsonAsync(
      new Uri("auth/passkeys/login/verify", UriKind.Relative),
      new { Credential = "credential-payload" });
    loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    AccessTokenResponseDto firstPair =
      (await loginResponse.Content.ReadFromJsonAsync<AccessTokenResponseDto>())!;

    HttpResponseMessage refreshResponse = await _client.PostAsJsonAsync(
      new Uri("auth/refresh", UriKind.Relative),
      new { firstPair.RefreshToken });

    refreshResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    AccessTokenResponseDto secondPair =
      (await refreshResponse.Content.ReadFromJsonAsync<AccessTokenResponseDto>())!;
    secondPair.AccessToken.ShouldNotBeNullOrWhiteSpace();
    secondPair.AccessToken.ShouldNotBe(firstPair.AccessToken);
    secondPair.RefreshToken.ShouldNotBeNullOrWhiteSpace();
  }

  [Fact]
  public async Task Refresh_WithGarbageToken_Returns401()
  {
    HttpResponseMessage response = await _client.PostAsJsonAsync(
      new Uri("auth/refresh", UriKind.Relative),
      new { RefreshToken = "not-a-real-token" });

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Refresh_WithEmptyBody_Returns401()
  {
    HttpResponseMessage response = await _client.PostAsJsonAsync(
      new Uri("auth/refresh", UriKind.Relative),
      new { RefreshToken = "" });

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Refresh_AfterExpiry_Returns401()
  {
    DateTimeOffset originalNow = _factory.TimeProvider.GetUtcNow();
    _factory.TimeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    try
    {
      Guid userId = await SeedUserAsync(username: "refresh-expiry-user");
      _factory.PasskeyAuthenticator.NextAssertion =
        Result.Success(new PasskeyAssertionOutcome(userId));

      HttpResponseMessage loginResponse = await _client.PostAsJsonAsync(
        new Uri("auth/passkeys/login/verify", UriKind.Relative),
        new { Credential = "credential-payload" });
      loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
      AccessTokenResponseDto pair =
        (await loginResponse.Content.ReadFromJsonAsync<AccessTokenResponseDto>())!;

      _factory.TimeProvider.Advance(TimeSpan.FromHours(13));

      HttpResponseMessage refreshResponse = await _client.PostAsJsonAsync(
        new Uri("auth/refresh", UriKind.Relative),
        new { pair.RefreshToken });

      refreshResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
    finally
    {
      _factory.TimeProvider.SetUtcNow(originalNow);
    }
  }

  [Fact]
  public async Task Logout_Unauthenticated_Returns401()
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("auth/logout", UriKind.Relative));
    HttpResponseMessage response = await _client.SendAsync(request);

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Logout_HappyPath_Returns204_AndSubsequentRefreshReturns401()
  {
    Guid userId = await SeedUserAsync(username: "logout-happy-user");
    _factory.PasskeyAuthenticator.NextAssertion =
      Result.Success(new PasskeyAssertionOutcome(userId));

    HttpResponseMessage loginResponse = await _client.PostAsJsonAsync(
      new Uri("auth/passkeys/login/verify", UriKind.Relative),
      new { Credential = "credential-payload" });
    loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    AccessTokenResponseDto pair =
      (await loginResponse.Content.ReadFromJsonAsync<AccessTokenResponseDto>())!;

    using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, new Uri("auth/logout", UriKind.Relative));
    logoutRequest.Headers.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", pair.AccessToken);
    HttpResponseMessage logoutResponse = await _client.SendAsync(logoutRequest);

    logoutResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    HttpResponseMessage refreshResponse = await _client.PostAsJsonAsync(
      new Uri("auth/refresh", UriKind.Relative),
      new { pair.RefreshToken });

    refreshResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Full_Lifecycle_Register_Login_Refresh_Refresh_Logout_FinalRefreshReturns401()
  {
    // 1. Seed a Manager directly (bootstrap - cannot register anyone without one).
    Guid managerId = await SeedUserAsync(username: "lifecycle-manager", role: Roles.Manager);

    // 2. Log in as the Manager.
    _factory.PasskeyAuthenticator.NextAssertion =
      Result.Success(new PasskeyAssertionOutcome(managerId));
    HttpResponseMessage managerLogin = await _client.PostAsJsonAsync(
      new Uri("auth/passkeys/login/verify", UriKind.Relative),
      new { Credential = "mgr-credential" });
    managerLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
    AccessTokenResponseDto managerPair =
      (await managerLogin.Content.ReadFromJsonAsync<AccessTokenResponseDto>())!;

    // 3a. Manager creates a new Receptionist account (no passkey yet).
    const string newUsername = "lifecycle-user";
    using var createUser = new HttpRequestMessage(HttpMethod.Post,
      new Uri("users", UriKind.Relative))
    {
      Content = JsonContent.Create(new
      {
        Username = newUsername,
        Name = "Lifecycle User",
        Role = Roles.Receptionist
      })
    };
    createUser.Headers.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", managerPair.AccessToken);
    HttpResponseMessage createUserResponse = await _client.SendAsync(createUser);
    createUserResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    CreatedUserDto created = (await createUserResponse.Content.ReadFromJsonAsync<CreatedUserDto>())!;
    Guid newUserId = created.Id;

    // 3b. Manager starts passkey registration for the new user.
    using var registerChallenge = new HttpRequestMessage(HttpMethod.Post,
      new Uri($"users/{newUserId}/passkeys/register/challenge", UriKind.Relative));
    registerChallenge.Headers.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", managerPair.AccessToken);
    HttpResponseMessage registerChallengeResponse = await _client.SendAsync(registerChallenge);
    registerChallengeResponse.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? regChallengeEx) ? regChallengeEx.ToString() : "no exception");

    _factory.PasskeyAuthenticator.NextAttestation =
      Result.Success(new PasskeyAttestationOutcome(newUserId));

    using var registerVerify = new HttpRequestMessage(HttpMethod.Post,
      new Uri($"users/{newUserId}/passkeys/register/verify", UriKind.Relative))
    {
      Content = JsonContent.Create(new { Credential = "attestation-payload" })
    };
    registerVerify.Headers.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", managerPair.AccessToken);
    HttpResponseMessage registerVerifyResponse = await _client.SendAsync(registerVerify);
    registerVerifyResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    // 4. Log in as the new user.
    _factory.PasskeyAuthenticator.NextAssertion =
      Result.Success(new PasskeyAssertionOutcome(newUserId));
    HttpResponseMessage userLogin = await _client.PostAsJsonAsync(
      new Uri("auth/passkeys/login/verify", UriKind.Relative),
      new { Credential = "user-credential" });
    userLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
    AccessTokenResponseDto userPair =
      (await userLogin.Content.ReadFromJsonAsync<AccessTokenResponseDto>())!;

    // 5. Refresh #1.
    HttpResponseMessage refresh1 = await _client.PostAsJsonAsync(
      new Uri("auth/refresh", UriKind.Relative),
      new { userPair.RefreshToken });
    refresh1.StatusCode.ShouldBe(HttpStatusCode.OK);
    AccessTokenResponseDto afterRefresh1 =
      (await refresh1.Content.ReadFromJsonAsync<AccessTokenResponseDto>())!;

    // 6. Refresh #2.
    HttpResponseMessage refresh2 = await _client.PostAsJsonAsync(
      new Uri("auth/refresh", UriKind.Relative),
      new { afterRefresh1.RefreshToken });
    refresh2.StatusCode.ShouldBe(HttpStatusCode.OK);
    AccessTokenResponseDto afterRefresh2 =
      (await refresh2.Content.ReadFromJsonAsync<AccessTokenResponseDto>())!;

    // 7. Logout using the most recent access token.
    using var logoutRequest = new HttpRequestMessage(HttpMethod.Post,
      new Uri("auth/logout", UriKind.Relative));
    logoutRequest.Headers.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", afterRefresh2.AccessToken);
    HttpResponseMessage logoutResponse = await _client.SendAsync(logoutRequest);
    logoutResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    // 8. Final refresh must fail.
    HttpResponseMessage finalRefresh = await _client.PostAsJsonAsync(
      new Uri("auth/refresh", UriKind.Relative),
      new { afterRefresh2.RefreshToken });
    finalRefresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Me_Unauthenticated_Returns401()
  {
    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("auth/me", UriKind.Relative));
    HttpResponseMessage response = await _client.SendAsync(request);

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Me_Authenticated_ReturnsCurrentUserIdentity()
  {
    Guid userId = await SeedUserAsync(username: "me-test-user", role: Roles.Receptionist);
    _factory.PasskeyAuthenticator.NextAssertion =
      Result.Success(new PasskeyAssertionOutcome(userId));

    HttpResponseMessage loginResponse = await _client.PostAsJsonAsync(
      new Uri("auth/passkeys/login/verify", UriKind.Relative),
      new { Credential = "credential-payload" });
    loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    AccessTokenResponseDto pair =
      (await loginResponse.Content.ReadFromJsonAsync<AccessTokenResponseDto>())!;

    using var meRequest = new HttpRequestMessage(HttpMethod.Get, new Uri("auth/me", UriKind.Relative));
    meRequest.Headers.Authorization =
      new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", pair.AccessToken);
    HttpResponseMessage meResponse = await _client.SendAsync(meRequest);

    meResponse.StatusCode.ShouldBe(HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");
    CurrentUserDto? body = await meResponse.Content.ReadFromJsonAsync<CurrentUserDto>();
    body.ShouldNotBeNull();
    body.Id.ShouldBe(userId);
    body.Username.ShouldBe("me-test-user");
    body.Roles.ShouldContain(Roles.Receptionist);
    body.SessionExpiresAt.ShouldNotBeNull();
    DateTimeOffset expectedDeadline = _factory.TimeProvider.GetUtcNow().AddHours(12);
    body.SessionExpiresAt.Value.ShouldBe(expectedDeadline, TimeSpan.FromSeconds(5));
  }

  [Fact]
  public async Task Refresh_AfterAbsoluteSessionDeadline_Returns401()
  {
    DateTimeOffset originalNow = _factory.TimeProvider.GetUtcNow();
    _factory.TimeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    try
    {
      Guid userId = await SeedUserAsync(username: "session-cap-user");
      _factory.PasskeyAuthenticator.NextAssertion =
        Result.Success(new PasskeyAssertionOutcome(userId));

      HttpResponseMessage loginResponse = await _client.PostAsJsonAsync(
        new Uri("auth/passkeys/login/verify", UriKind.Relative),
        new { Credential = "credential-payload" });
      loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
      AccessTokenResponseDto pair =
        (await loginResponse.Content.ReadFromJsonAsync<AccessTokenResponseDto>())!;

      // Two refreshes so the token's own expiry never lapses; only the absolute 12h session
      // deadline should fire.
      _factory.TimeProvider.Advance(TimeSpan.FromHours(6));
      HttpResponseMessage midRefresh = await _client.PostAsJsonAsync(
        new Uri("auth/refresh", UriKind.Relative),
        new { pair.RefreshToken });
      midRefresh.StatusCode.ShouldBe(HttpStatusCode.OK,
        _factory.ServerExceptions.TryPeek(out Exception? midEx) ? midEx.ToString() : "no exception");
      AccessTokenResponseDto midPair =
        (await midRefresh.Content.ReadFromJsonAsync<AccessTokenResponseDto>())!;

      _factory.TimeProvider.Advance(TimeSpan.FromHours(6).Add(TimeSpan.FromMinutes(1)));
      HttpResponseMessage capRefresh = await _client.PostAsJsonAsync(
        new Uri("auth/refresh", UriKind.Relative),
        new { midPair.RefreshToken });

      capRefresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
    finally
    {
      _factory.TimeProvider.SetUtcNow(originalNow);
    }
  }

  private async Task<Guid> SeedUserAsync(string username, string? role = null)
  {
    var userId = Guid.NewGuid();
    var user = new ApplicationUser
    {
      Id = userId,
      UserName = username,
      NormalizedUserName = username.ToUpperInvariant(),
      SecurityStamp = Guid.NewGuid().ToString()
    };

    await _factory.WithDbAsync(async db =>
    {
      db.Users.Add(user);
      await db.SaveChangesAsync();
    });

    if (role is not null)
    {
      await _factory.WithScopeAsync(async sp =>
      {
        UserManager<ApplicationUser> userManager =
          sp.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser? tracked = await userManager.FindByIdAsync(userId.ToString())
          ?? throw new InvalidOperationException(
            $"Seeded user {username} was not found when trying to assign role {role}.");
        IdentityResult roled = await userManager.AddToRoleAsync(tracked, role);
        if (!roled.Succeeded)
        {
          throw new InvalidOperationException(
            "Role seed failed: " + string.Join("; ", roled.Errors.Select(e => e.Description)));
        }
      });
    }

    return userId;
  }

  private sealed record AccessTokenResponseDto(
    string TokenType,
    string AccessToken,
    int ExpiresIn,
    string RefreshToken);

  private sealed record CreatedUserDto(Guid Id);

  private sealed record CurrentUserDto(
    Guid Id,
    string Username,
    string Name,
    IReadOnlyList<string> Roles,
    DateTimeOffset? SessionExpiresAt);
}
