using Application.Abstractions.Messaging;

namespace Application.Auth.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery : IQuery<CurrentUserResponse>;

public sealed record CurrentUserResponse(
  Guid Id,
  string Username,
  string Name,
  IReadOnlyList<string> Roles,
  DateTimeOffset? SessionExpiresAt);
