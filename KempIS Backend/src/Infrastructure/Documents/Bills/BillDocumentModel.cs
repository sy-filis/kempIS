namespace Infrastructure.Documents.Bills;

public sealed record CampLegalInfo(
  string Name,
  string Street,
  string CityZip,
  string Cin,
  string Tin,
  string Phone,
  string Email,
  string Web);

public sealed record BillDocumentModel(
  string Number,
  DateTime IssuedAtUtc,
  string LanguageCode,
  bool IsRepair,
  string? RepairReason,
  CampLegalInfo Camp,
  BillDocumentPartyModel Payer,
  BillDocumentLegalEntityModel? LegalEntity,
  IReadOnlyList<BillDocumentLineModel> Lines,
  decimal Total,
  string PaymentType,
  IReadOnlyList<BillDocumentDeductionModel> Deductions,
  IReadOnlyList<BillDocumentVatRecapLineModel> VatRecap,
  decimal GrossSubtotal);

public sealed record BillDocumentPartyModel(string Name, string? Street, string? City, string? PostalCode, string? Country);

public sealed record BillDocumentLegalEntityModel(string Name, string Cin, string? Tin, BillDocumentPartyModel Address);

public sealed record BillDocumentLineModel(
  string Description,
  decimal NetUnitPrice,
  decimal UnitPrice,
  uint RecapSingleQuantity,
  uint RecapDayQuantity,
  decimal VatRatePercentage,
  decimal LineTotal,
  string? VehiclesAndSpotsSuffix);

public sealed record BillDocumentDeductionModel(
  string InvoiceNumber,
  IReadOnlyList<BillDocumentVatRecapLineModel> VatRecap,
  decimal DeductedAmount);

public sealed record BillDocumentVatRecapLineModel(
  decimal VatRatePercentage,
  decimal NetTotal,
  decimal VatAmount,
  decimal GrossTotal);
