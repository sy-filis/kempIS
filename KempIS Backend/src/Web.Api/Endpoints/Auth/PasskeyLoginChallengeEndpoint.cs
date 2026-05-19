using Application.Abstractions.Messaging;
using Application.Auth.Queries.PasskeyLoginChallenge;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Auth;

internal sealed class PasskeyLoginChallengeEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("auth/passkeys/login/challenge", async (
      IQueryHandler<PasskeyLoginChallengeQuery, string> handler,
      CancellationToken cancellationToken) =>
    {
      Result<string> result = await handler.Handle(new PasskeyLoginChallengeQuery(), cancellationToken);

      return result.Match(
        json => Results.Content(json, "application/json"),
        CustomResults.Problem);
    })
    .WithTags(Tags.Auth)
    .WithName("PasskeyLoginChallenge")
    .WithSummary("Begin a passkey login (returns the WebAuthn challenge)")
    .WithDescription("""
      Starts a passkey-based login by returning a fresh WebAuthn assertion options object.
      The browser passes this payload to `navigator.credentials.get(...)` and the resulting
      assertion is then submitted to `auth/passkeys/login/verify`. The endpoint is anonymous
      because callers have not yet authenticated.

      **Behavior:** returns a freshly minted WebAuthn challenge tied to the configured relying
      party - no user is identified at this point; resident-key (discoverable credential)
      flows are supported.

      **Response body:** the body is a serialized WebAuthn `PublicKeyCredentialRequestOptions`
      JSON object produced by ASP.NET Identity's passkey support, ready to feed into the
      browser's WebAuthn API.
      """)
    .Produces<string>(StatusCodes.Status200OK, contentType: "application/json")
    .AllowAnonymous();
  }
}
