using Domain.Finance.Bills;
using Domain.Finance.Payments;

namespace Application.Finance.FinancialClosings.GetFinancialClosing;

public sealed record FinancialClosingDetailResponse(
  Guid Id,
  uint FinancialClosingId,
  DateTime ClosedAtUtc,
  Guid? CreatedByUserId,
  IReadOnlyList<FinancialClosingBillItem> Bills,
  FinancialClosingPaymentTotals PaymentTotals,
  IReadOnlyList<FinancialClosingVatRecapRow> VatRecap,
  IReadOnlyList<FinancialClosingVatRecapByServiceTypeRow> VatRecapByServiceType);

public sealed record FinancialClosingBillItem(
  Guid Id,
  string Number,
  DateTime IssuedAtUtc,
  string PayerName,
  PaymentType PaymentType,
  decimal Total,
  BillKind Kind,
  Guid? OriginalBillId);

public sealed record FinancialClosingPaymentTotals(
  decimal Cash,
  decimal Card,
  decimal Total);

public sealed record FinancialClosingVatRecapRow(
  decimal VatRatePercentage,
  decimal Net,
  decimal Vat,
  decimal Gross);

public sealed record FinancialClosingVatRecapByServiceTypeRow(
  string ServiceTypeName,
  decimal VatRatePercentage,
  decimal Net,
  decimal Vat,
  decimal Gross);
