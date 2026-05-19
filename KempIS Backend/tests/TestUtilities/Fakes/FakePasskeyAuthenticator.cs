using Application.Abstractions.Authentication;
using SharedKernel;

namespace TestUtilities.Fakes;

public sealed class FakePasskeyAuthenticator : IPasskeyAuthenticator
{
  public Result<string>? NextRegistrationOptions { get; set; }
  public Result<PasskeyAttestationOutcome>? NextAttestation { get; set; }
  public string? NextAssertionOptions { get; set; }
  public Result<PasskeyAssertionOutcome>? NextAssertion { get; set; }

  public Guid? LastRegistrationUserId { get; private set; }
  public string? LastRegistrationUsername { get; private set; }
  public string? LastRegistrationDisplayName { get; private set; }
  public string? LastAttestationCredentialJson { get; private set; }
  public string? LastAttestationName { get; private set; }
  public string? LastAssertionCredentialJson { get; private set; }

  public Task<Result<string>> CreateRegistrationOptionsAsync(
      Guid userId,
      string username,
      string displayName,
      CancellationToken cancellationToken)
  {
    LastRegistrationUserId = userId;
    LastRegistrationUsername = username;
    LastRegistrationDisplayName = displayName;
    return Task.FromResult(NextRegistrationOptions ?? Result.Success("{\"options\":\"fake\"}"));
  }

  public Task<Result<PasskeyAttestationOutcome>> VerifyAttestationAsync(
      string credentialJson,
      string? name,
      CancellationToken cancellationToken)
  {
    LastAttestationCredentialJson = credentialJson;
    LastAttestationName = name;
    return Task.FromResult(NextAttestation ?? Result.Success(new PasskeyAttestationOutcome(Guid.NewGuid())));
  }

  public Task<string> CreateAssertionOptionsAsync(CancellationToken cancellationToken)
      => Task.FromResult(NextAssertionOptions ?? "{\"challenge\":\"fake\"}");

  public Task<Result<PasskeyAssertionOutcome>> VerifyAssertionAsync(
      string credentialJson,
      CancellationToken cancellationToken)
  {
    LastAssertionCredentialJson = credentialJson;
    return Task.FromResult(
      NextAssertion
      ?? Result.Success(new PasskeyAssertionOutcome(Guid.NewGuid())));
  }
}
