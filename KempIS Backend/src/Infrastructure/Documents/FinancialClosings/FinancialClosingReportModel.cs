namespace Infrastructure.Documents.FinancialClosings;

public sealed record FinancialClosingReportModel(
  uint SequentialNumber,
  DateTime ClosedAtUtc,
  string CashierLabel,
  string CampName,
  string CampStreet,
  string CampCityZip,
  IReadOnlyList<string> ServiceTypeColumns,
  IReadOnlyList<decimal> VatRates,
  IReadOnlyList<PaymentSection> Sections,
  ReportFooter Footer);

public sealed record PaymentSection(
  string Title,
  IReadOnlyList<BillRow> Bills,
  BillRow Subtotal,
  BillRow DocumentTotal);

public sealed record BillRow(
  string BillNumber,
  bool IsStorno,
  IReadOnlyDictionary<string, decimal> ServiceTypeAmounts,
  decimal Total,
  IReadOnlyDictionary<decimal, decimal> VatBases,
  IReadOnlyDictionary<decimal, decimal> VatAmounts);

public sealed record ReportFooter(
  decimal TotalNet,
  decimal TotalVat,
  decimal GrandTotal);
