using Application.Abstractions.Messaging;
using Domain.Finance.Bills;
using Domain.Finance.Payments;

namespace Application.Finance.Bills.ListBills;

public sealed record ListBillsQuery(
  DateOnly? From,
  DateOnly? To,
  Guid? ReservationId,
  BillKind? Kind,
  Guid? FinancialClosingId,
  bool? Closed)
  : IQuery<IReadOnlyList<BillSummary>>;

public sealed record BillSummary(
  Guid Id, string Number, BillKind Kind, Guid? ReservationId,
  DateOnly CheckInAt, DateOnly CheckOutAt, DateTime IssuedAtUtc, decimal Amount,
  Guid? FinancialClosingId, PaymentType PaymentType);
