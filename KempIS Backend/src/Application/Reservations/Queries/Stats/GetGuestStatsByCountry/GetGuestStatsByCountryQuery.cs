using Application.Abstractions.Messaging;

namespace Application.Reservations.Queries.Stats.GetGuestStatsByCountry;

public sealed record GetGuestStatsByCountryQuery(DateOnly From, DateOnly To)
  : IQuery<GuestStatsByCountryResponse>;

public sealed record GuestStatsByCountryResponse(
  DateOnly From,
  DateOnly To,
  int TotalGuests,
  int TotalPersonNights,
  IReadOnlyList<GuestStatsByCountryRow> Rows);

public sealed record GuestStatsByCountryRow(
  Guid NationalityId,
  string Alpha2,
  string Alpha3,
  string Name,
  string NameEn,
  int GuestCount,
  int PersonNights);
