using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Auth.Queries.PasskeyLoginChallenge;

internal sealed class PasskeyLoginChallengeQueryHandler(
    IPasskeyAuthenticator passkeyAuthenticator)
  : IQueryHandler<PasskeyLoginChallengeQuery, string>
{
  public async Task<Result<string>> Handle(
      PasskeyLoginChallengeQuery query,
      CancellationToken cancellationToken) =>
      await passkeyAuthenticator.CreateAssertionOptionsAsync(cancellationToken);
}
