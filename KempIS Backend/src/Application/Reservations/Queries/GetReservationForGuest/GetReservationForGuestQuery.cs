using Application.Abstractions.Messaging;
using Application.Reservations.Meals;
using Domain.Finance.Bills;

namespace Application.Reservations.Queries.GetReservationForGuest;

public sealed record GetReservationForGuestQuery(Guid Id, string Secret)
  : IQuery<ReservationForGuestResponse>;

public sealed record ReservationForGuestResponse(
  Guid Id,
  string Number,
  string State,
  DateOnly From,
  DateOnly To,
  string Name,
  string Surname,
  string? Note,
  Guid? GroupReservationId,
  IReadOnlyList<ReservationForGuestSpotItem> SpotItems,
  IReadOnlyList<ReservationForGuestMeal> Meals,
  IReadOnlyList<ReservationForGuestBill> Bills);

public sealed record ReservationForGuestSpotItem(
  Guid SpotGroupId,
  string SpotGroupName,
  Guid? SpotId,
  string? SpotName,
  IReadOnlyList<ReservationForGuestGroupSpot> GroupSpots,
  IReadOnlyList<ReservationForGuestServiceText> ServiceTexts);

public sealed record ReservationForGuestGroupSpot(Guid Id, string Name);

public sealed record ReservationForGuestServiceText(Guid LanguageId, string PrintText);

public sealed record ReservationForGuestMeal(
  DateOnly Date,
  MealAmountDto Breakfast,
  MealAmountDto Lunch,
  MealAmountDto LunchPackage,
  MealAmountDto Dinner);

public sealed record ReservationForGuestBill(
  Guid Id,
  string Number,
  BillKind Kind,
  decimal Amount);
