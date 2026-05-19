using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Auth.Commands.RegisterPasskeyVerify;

internal sealed class RegisterPasskeyVerifyCommandHandler(
    IPasskeyAuthenticator passkeyAuthenticator)
  : ICommandHandler<RegisterPasskeyVerifyCommand>
{
  public async Task<Result> Handle(
      RegisterPasskeyVerifyCommand command,
      CancellationToken cancellationToken)
  {
    Result<PasskeyAttestationOutcome> outcome =
        await passkeyAuthenticator.VerifyAttestationAsync(
            command.Credential, command.Name, cancellationToken);

    return outcome.IsFailure ? Result.Failure(outcome.Error) : Result.Success();
  }
}
