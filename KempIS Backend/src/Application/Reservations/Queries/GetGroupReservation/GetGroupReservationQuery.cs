using Application.Abstractions.Messaging;

namespace Application.Reservations.Queries.GetGroupReservation;

public sealed record GetGroupReservationQuery(Guid Id) : IQuery<GroupReservationResponse>;

public sealed record GroupReservationResponse(
  Guid Id,
  string Number,
  string State,
  DateOnly From,
  DateOnly To,
  string Secret,
  string OrganizerName,
  string OrganizerEmail,
  string OrganizerPhone,
  string? Note,
  DateTime CreatedAtUtc,
  DateTime? UpdatedAtUtc,
  IReadOnlyList<Guid> SpotIds,
  string? DisplayName);
