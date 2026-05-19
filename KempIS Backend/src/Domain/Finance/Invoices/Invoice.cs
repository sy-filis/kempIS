using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using SharedKernel;

namespace Domain.Finance.Invoices;

public sealed class Invoice : Entity
{
  public Guid Id { get; set; }

  public Guid ReservationId { get; set; }

  public string? Number { get; set; }

  public InvoiceStatus Status { get; set; }

  public DateOnly IssuedAt { get; set; }

  public DateOnly? PaidAt { get; set; }

  public DateOnly? DueTo { get; set; }

  public Guid? LinkedBillId { get; set; }

  public string Email { get; set; } = null!;

  public string PhoneNumber { get; set; } = null!;

  public Payer? Payer { get; set; }

  public LegalEntity? LegalEntity { get; set; }

  /// <summary>Null means legal hold; cleared by the retention job after wipe.</summary>
  public DateOnly? Scartation { get; set; }
}
