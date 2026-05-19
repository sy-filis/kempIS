using Application.Abstractions.Authentication;
using Infrastructure.Identity;
using SharedKernel;
using Web.Api.IntegrationTests.Infrastructure;

namespace Web.Api.IntegrationTests.Auth;

public sealed class AuthEndpointTests : IClassFixture<ApiFactory>
{
  private readonly ApiFactory _factory;
  private readonly HttpClient _client;

  public AuthEndpointTests(ApiFactory factory)
  {
    _factory = factory;
    _client = factory.CreateClient();
  }

  [Fact]
  public async Task GetLoginChallenge_Anonymous_Returns200WithJson()
  {
    _factory.PasskeyAuthenticator.NextAssertionOptions = "{\"challenge\":\"abc\"}";

    HttpResponseMessage response = await _client.GetAsync(
      new Uri("auth/passkeys/login/challenge", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    string body = await response.Content.ReadAsStringAsync();
    body.ShouldBe("{\"challenge\":\"abc\"}");
  }

  [Fact]
  public async Task PostLoginVerify_VerificationSucceeds_Returns200WithToken()
  {
    var userId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.Users.Add(new ApplicationUser
      {
        Id = userId,
        UserName = "login-test-user",
        NormalizedUserName = "LOGIN-TEST-USER",
        SecurityStamp = Guid.NewGuid().ToString()
      });
      await db.SaveChangesAsync();
    });

    _factory.PasskeyAuthenticator.NextAssertion =
      Result.Success(new PasskeyAssertionOutcome(userId));

    HttpResponseMessage response = await _client.PostAsJsonAsync(
      new Uri("auth/passkeys/login/verify", UriKind.Relative),
      new { Credential = "credential-payload" });

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    LoginResponse? body = await response.Content.ReadFromJsonAsync<LoginResponse>();
    body.ShouldNotBeNull();
    body.AccessToken.ShouldNotBeNullOrWhiteSpace();
  }

  [Fact]
  public async Task PostLoginVerify_VerificationFails_Returns400OrProblem()
  {
    _factory.PasskeyAuthenticator.NextAssertion =
      Result.Failure<PasskeyAssertionOutcome>(Error.Problem("Auth.AssertionInvalid", "bad sig"));

    HttpResponseMessage response = await _client.PostAsJsonAsync(
      new Uri("auth/passkeys/login/verify", UriKind.Relative),
      new { Credential = "credential-payload" });

    response.IsSuccessStatusCode.ShouldBeFalse();
  }

  [Fact]
  public async Task PostLoginVerify_EmptyCredential_Returns400FromValidator()
  {
    HttpResponseMessage response = await _client.PostAsJsonAsync(
      new Uri("auth/passkeys/login/verify", UriKind.Relative),
      new { Credential = "" });

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  private sealed record LoginResponse(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn);
}
