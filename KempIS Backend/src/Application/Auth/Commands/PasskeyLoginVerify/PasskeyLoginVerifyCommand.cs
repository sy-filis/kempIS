using Application.Abstractions.Messaging;

namespace Application.Auth.Commands.PasskeyLoginVerify;

public sealed record PasskeyLoginVerifyCommand(string Credential) : ICommand<Guid>;
