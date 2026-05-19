using Application.Abstractions.Messaging;
using Application.Reservations.Guests;
using Domain.Common;
using Domain.Finance.Bills;
using Domain.Finance.Payments;

namespace Application.Finance.Bills.GetBillById;

public sealed record GetBillByIdQuery(Guid BillId) : IQuery<GetBillByIdResponse>;

public sealed record BillPayerView(string Name, string Surname, Address Address);
public sealed record BillLegalEntityView(string Name, string Cin, string? Tin, Address Address);
public sealed record BillPaymentView(PaymentType PaymentType, decimal Amount);
public sealed record BillItemView(
  Guid Id, Guid? ServiceId, uint Quantity, decimal UnitPrice,
  decimal VatRatePercentage, uint RecapSingleQuantity, uint RecapDayQuantity);
public sealed record BillDeductionView(Guid InvoiceId, string? InvoiceNumber, decimal Amount);
public sealed record BillRepairSummary(Guid Id, string Number, DateTime IssuedAtUtc, decimal Amount, string? Reason);
public sealed record BillVehicleView(Guid Id, string RegistrationNumber, Guid? ServiceId);
public sealed record BillSpotItemView(Guid Id, Guid? SpotId, bool HasGivenKey, bool HasReturnedKeys);
public sealed record BillAccessCardView(Guid Id, ulong Uid, decimal Deposit, DateTime IssuedAtUtc, string? Note);

public sealed record GetBillByIdResponse(
  Guid Id, string Number, BillKind Kind, Guid? OriginalBillId, string? RepairReason, Guid? ReservationId,
  Guid LanguageId, DateTime IssuedAtUtc, DateOnly CheckInAt, DateOnly CheckOutAt,
  BillPayerView Payer, BillLegalEntityView? LegalEntity, BillPaymentView Payment,
  IReadOnlyList<BillItemView> Items,
  IReadOnlyList<BillDeductionView> Deductions,
  IReadOnlyList<BillRepairSummary> Repairs,
  IReadOnlyList<GuestDetailResponse> Guests,
  IReadOnlyList<BillVehicleView> Vehicles,
  IReadOnlyList<BillSpotItemView> SpotItems,
  IReadOnlyList<BillAccessCardView> AccessCards);
