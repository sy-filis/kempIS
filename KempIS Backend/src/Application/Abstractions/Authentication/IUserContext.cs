namespace Application.Abstractions.Authentication;

public interface IUserContext
{
  Guid UserId { get; }

  DateTimeOffset? SessionExpiresAt { get; }
}
