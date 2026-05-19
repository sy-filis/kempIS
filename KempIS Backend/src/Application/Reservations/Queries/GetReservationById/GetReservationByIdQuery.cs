using Application.Abstractions.Messaging;
using Application.Reservations.Meals;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.Invoices;
using Domain.Finance.Payments;
using Domain.Reservations.Guests;
using Domain.Reservations.ReservationStates;

namespace Application.Reservations.Queries.GetReservationById;

public sealed record GetReservationByIdQuery(Guid Id) : IQuery<ReservationDetailResponse>;

public sealed record ReservationDetailResponse(
  Guid Id,
  string Number,
  string Secret,
  ReservationState State,
  DateOnly From,
  DateOnly To,
  string ReservationMakerName,
  string ReservationMakerSurname,
  string ReservationMakerEmail,
  string ReservationMakerPhone,
  Guid? GroupReservationId,
  string? Note,
  DateTime CreatedAtUtc,
  DateTime? UpdatedAtUtc,
  IReadOnlyList<ReservationDetailGuest> Guests,
  IReadOnlyList<ReservationDetailSpotItem> SpotItems,
  IReadOnlyList<ReservationDetailServiceItem> ServiceItems,
  IReadOnlyList<ReservationDetailVehicle> Vehicles,
  IReadOnlyList<ReservationDetailMeal> Meals,
  IReadOnlyList<ReservationDetailInvoice> Invoices,
  IReadOnlyList<ReservationDetailBill> Bills,
  IReadOnlyList<ReservationDetailAccessCard> AccessCards,
  string? DisplayName);

public sealed record ReservationDetailGuest(
  Guid Id,
  Guid? BillId,
  bool? PaysRecreationFee,
  string FirstName,
  string LastName,
  Guid NationalityId,
  DateOnly DateOfBirth,
  DocumentType? DocumentType,
  string? DocumentNumber,
  Address Address,
  string ReasonOfStay,
  DateOnly? StayFrom,
  DateOnly? StayTo,
  string? VisaNumber,
  string? Note,
  DateOnly? Scartation,
  DateTime? CheckInAt,
  DateTime? CheckOutAt,
  bool HasSignature,
  DateTime? SignatureCapturedAtUtc,
  DateTime? ReportedAt);

public sealed record ReservationDetailSpotItem(
  Guid Id,
  Guid SpotGroupId,
  Guid? SpotId,
  bool HasGivenKey,
  bool HasReturnedKeys,
  Guid? BillId);

public sealed record ReservationDetailServiceItem(
  Guid Id,
  Guid ServiceId,
  uint Quantity,
  uint RecapSingleQuantity,
  uint RecapDayQuantity);

public sealed record ReservationDetailVehicle(
  Guid Id,
  Guid? BillId,
  Guid? ServiceId,
  string RegistrationNumber);

public sealed record ReservationDetailMeal(
  DateOnly Date,
  MealAmountDto Breakfast,
  MealAmountDto Lunch,
  MealAmountDto LunchPackage,
  MealAmountDto Dinner);

public sealed record ReservationDetailInvoice(
  Guid Id,
  string? Number,
  InvoiceStatus Status,
  DateOnly IssuedAt,
  DateOnly? PaidAt,
  Guid? LinkedBillId);

public sealed record ReservationDetailBill(
  Guid Id,
  string Number,
  BillKind Kind,
  DateTime IssuedAtUtc,
  PaymentType PaymentType,
  decimal Amount);

public sealed record ReservationDetailAccessCard(
  Guid Id,
  ulong Uid,
  decimal Deposit,
  DateTime IssuedAtUtc);
