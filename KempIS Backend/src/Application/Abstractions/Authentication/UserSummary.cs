namespace Application.Abstractions.Authentication;

public sealed record UserSummary(
  Guid Id,
  string Username,
  string Name,
  IReadOnlyList<string> Roles,
  bool IsDisabled,
  DateTime CreatedAtUtc);

public sealed record UserDetail(
  Guid Id,
  string Username,
  string Name,
  IReadOnlyList<string> Roles,
  bool IsDisabled,
  DateTime CreatedAtUtc,
  int PasskeyCount);

public sealed record PasskeySummary(
  Guid Id,
  string? DisplayName,
  DateTime CreatedAtUtc);
