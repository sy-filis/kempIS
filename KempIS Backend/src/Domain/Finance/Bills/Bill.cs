using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using SharedKernel;

namespace Domain.Finance.Bills;

public sealed class Bill : Entity
{
  public Guid Id { get; set; }

  public Guid? ReservationId { get; set; }

  public BillKind Kind { get; set; } = BillKind.Regular;

  public Guid? OriginalBillId { get; set; }

  public string? RepairReason { get; set; }

  public Guid LanguageIdGuid { get; set; }

  public Guid? FinancialClosingId { get; set; }

  public string Number { get; set; }

  public DateTime IssuedAtUtc { get; set; }

  public DateOnly CheckInAt { get; set; }

  public DateOnly CheckOutAt { get; set; }

  public byte[]? DocumentContent { get; set; }

  public DateTime? DocumentGeneratedAtUtc { get; set; }

  public Payer Payer { get; set; }

  public LegalEntity? LegalEntity { get; set; }

  public Payment Payment { get; set; }

  /// <summary>Null means legal hold; cleared by the retention job after wipe.</summary>
  public DateOnly? Scartation { get; set; }
}
