using SharedKernel;

namespace Application.Abstractions.Authentication;

public interface IPasskeyAuthenticator
{
  Task<Result<string>> CreateRegistrationOptionsAsync(
      Guid userId,
      string username,
      string displayName,
      CancellationToken cancellationToken);

  Task<Result<PasskeyAttestationOutcome>> VerifyAttestationAsync(
      string credentialJson,
      string? name,
      CancellationToken cancellationToken);

  Task<string> CreateAssertionOptionsAsync(CancellationToken cancellationToken);

  Task<Result<PasskeyAssertionOutcome>> VerifyAssertionAsync(
      string credentialJson,
      CancellationToken cancellationToken);
}

public sealed record PasskeyAttestationOutcome(Guid UserId);

public sealed record PasskeyAssertionOutcome(Guid UserId);
