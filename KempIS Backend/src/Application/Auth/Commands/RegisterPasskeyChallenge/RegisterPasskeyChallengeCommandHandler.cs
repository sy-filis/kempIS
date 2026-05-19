using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Auth.Commands.RegisterPasskeyChallenge;

internal sealed class RegisterPasskeyChallengeCommandHandler(
    IIdentityService identityService,
    IPasskeyAuthenticator passkeyAuthenticator)
  : ICommandHandler<RegisterPasskeyChallengeCommand, string>
{
  public async Task<Result<string>> Handle(
      RegisterPasskeyChallengeCommand command,
      CancellationToken cancellationToken)
  {
    Result<UserDetail> user = await identityService.GetUserAsync(command.UserId, cancellationToken);
    if (user.IsFailure)
    {
      return Result.Failure<string>(user.Error);
    }

    return await passkeyAuthenticator.CreateRegistrationOptionsAsync(
        command.UserId, user.Value.Username, user.Value.Name, cancellationToken);
  }
}
