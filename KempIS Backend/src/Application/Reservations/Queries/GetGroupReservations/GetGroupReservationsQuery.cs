using Application.Abstractions.Messaging;
using Domain.Reservations.GroupReservations;

namespace Application.Reservations.Queries.GetGroupReservations;

public sealed record GetGroupReservationsQuery(
  DateOnly From,
  DateOnly To,
  GroupReservationState? State = null) : IQuery<List<GroupReservationListItemResponse>>;

public sealed record GroupReservationListItemResponse(
  Guid Id,
  string Number,
  string State,
  DateOnly From,
  DateOnly To,
  string OrganizerName,
  string OrganizerEmail,
  string OrganizerPhone,
  IReadOnlyList<Guid> SpotIds,
  DateTime CreatedAtUtc,
  string? DisplayName);
