using Application.Abstractions.Messaging;

namespace Application.Auth.Commands.RegisterPasskeyVerify;

public sealed record RegisterPasskeyVerifyCommand(
  string Credential,
  string? Name = null) : ICommand;
