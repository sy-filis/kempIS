using Application.Abstractions.Authentication;

namespace Web.Api.Authentication;

internal sealed class NoAuthOptions : INoAuthState
{
  public required bool IsEnabled { get; init; }
}
