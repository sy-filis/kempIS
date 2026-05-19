using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Auth.Commands.PasskeyLoginVerify;

internal sealed class PasskeyLoginVerifyCommandHandler(
    IPasskeyAuthenticator passkeyAuthenticator)
  : ICommandHandler<PasskeyLoginVerifyCommand, Guid>
{
  public async Task<Result<Guid>> Handle(
      PasskeyLoginVerifyCommand command,
      CancellationToken cancellationToken)
  {
    Result<PasskeyAssertionOutcome> outcome =
        await passkeyAuthenticator.VerifyAssertionAsync(
            command.Credential, cancellationToken);

    return outcome.IsFailure
        ? Result.Failure<Guid>(outcome.Error)
        : outcome.Value.UserId;
  }
}
