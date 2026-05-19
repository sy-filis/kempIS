using Application.Abstractions.Messaging;

namespace Application.Auth.Commands.RegisterPasskeyChallenge;

public sealed record RegisterPasskeyChallengeCommand(Guid UserId)
  : ICommand<string>;
