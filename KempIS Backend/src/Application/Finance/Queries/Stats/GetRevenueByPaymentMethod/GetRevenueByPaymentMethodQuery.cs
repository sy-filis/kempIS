using Application.Abstractions.Messaging;

namespace Application.Finance.Queries.Stats.GetRevenueByPaymentMethod;

public sealed record GetRevenueByPaymentMethodQuery(DateOnly From, DateOnly To)
  : IQuery<RevenueByPaymentMethodResponse>;

public sealed record RevenueByPaymentMethodResponse(
  DateOnly From,
  DateOnly To,
  int TotalBillCount,
  decimal TotalGross,
  IReadOnlyList<RevenueByPaymentMethodRow> Rows);

public sealed record RevenueByPaymentMethodRow(
  string PaymentType,
  int BillCount,
  decimal Gross,
  decimal SharePercent);
