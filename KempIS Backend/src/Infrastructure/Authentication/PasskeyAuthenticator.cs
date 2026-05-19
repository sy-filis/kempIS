using Application.Abstractions.Authentication;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using SharedKernel;

namespace Infrastructure.Authentication;

internal sealed class PasskeyAuthenticator(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager)
  : IPasskeyAuthenticator
{
  public async Task<Result<string>> CreateRegistrationOptionsAsync(
      Guid userId,
      string username,
      string displayName,
      CancellationToken cancellationToken)
  {
    string optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(
        new PasskeyUserEntity
        {
          Id = userId.ToString(),
          Name = username,
          DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName
        });

    return optionsJson;
  }

  public async Task<Result<PasskeyAttestationOutcome>> VerifyAttestationAsync(
      string credentialJson,
      string? name,
      CancellationToken cancellationToken)
  {
    PasskeyAttestationResult attestation =
        await signInManager.PerformPasskeyAttestationAsync(credentialJson);

    if (!attestation.Succeeded)
    {
      return Result.Failure<PasskeyAttestationOutcome>(
          AuthErrors.IdentityFailure(
              attestation.Failure?.Message ?? "Passkey attestation failed."));
    }

    if (!Guid.TryParse(attestation.UserEntity.Id, out Guid userId))
    {
      return Result.Failure<PasskeyAttestationOutcome>(AuthErrors.UserNotFound);
    }

    ApplicationUser? user = await userManager.FindByIdAsync(userId.ToString());
    if (user is null)
    {
      return Result.Failure<PasskeyAttestationOutcome>(AuthErrors.UserNotFound);
    }

    if (!string.IsNullOrWhiteSpace(name))
    {
      attestation.Passkey.Name = name;
    }

    IdentityResult storeResult = await userManager.AddOrUpdatePasskeyAsync(user, attestation.Passkey);
    if (!storeResult.Succeeded)
    {
      return Result.Failure<PasskeyAttestationOutcome>(
          AuthErrors.IdentityFailure(
              string.Join("; ", storeResult.Errors.Select(e => e.Description))));
    }

    return new PasskeyAttestationOutcome(userId);
  }

  public Task<string> CreateAssertionOptionsAsync(CancellationToken cancellationToken) =>
      signInManager.MakePasskeyRequestOptionsAsync(user: null);

  public async Task<Result<PasskeyAssertionOutcome>> VerifyAssertionAsync(
      string credentialJson,
      CancellationToken cancellationToken)
  {
    PasskeyAssertionResult<ApplicationUser> assertion =
        await signInManager.PerformPasskeyAssertionAsync(credentialJson);

    if (!assertion.Succeeded)
    {
      return Result.Failure<PasskeyAssertionOutcome>(
          AuthErrors.IdentityFailure(
              assertion.Failure?.Message ?? "Passkey assertion failed."));
    }

    IdentityResult updateResult = await userManager.AddOrUpdatePasskeyAsync(assertion.User, assertion.Passkey);
    if (!updateResult.Succeeded)
    {
      return Result.Failure<PasskeyAssertionOutcome>(
          AuthErrors.IdentityFailure(
              string.Join("; ", updateResult.Errors.Select(e => e.Description))));
    }

    return new PasskeyAssertionOutcome(assertion.User.Id);
  }
}
