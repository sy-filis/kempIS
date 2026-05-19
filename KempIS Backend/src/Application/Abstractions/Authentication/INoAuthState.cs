namespace Application.Abstractions.Authentication;

public interface INoAuthState
{
  bool IsEnabled { get; }
}
