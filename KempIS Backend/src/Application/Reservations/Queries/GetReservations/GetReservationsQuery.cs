using Application.Abstractions.Messaging;
using Domain.Reservations.ReservationStates;

namespace Application.Reservations.Queries.GetReservations;

public sealed record GetReservationsQuery(
  DateOnly? From,
  DateOnly? To,
  ReservationState? Status = null) : IQuery<List<ReservationResponse>>;
