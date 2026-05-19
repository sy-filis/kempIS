using SharedKernel;

namespace Domain.Finance.FinancialClosings;

public sealed class FinancialClosing : Entity
{
  public Guid Id { get; set; }

  public DateTime ClosedAtUtc { get; set; }

  public uint FinancialClosingId { get; set; }

  public decimal TotalAmount { get; set; }

  public byte[]? DocumentContent { get; set; }

  public DateTime? DocumentGeneratedAtUtc { get; set; }

  public Guid? CreatedByUserId { get; set; }

  public static FinancialClosing Close(
    uint sequentialId,
    DateTime closedAtUtc,
    decimal totalAmount,
    Guid createdByUserId)
  {
    return new FinancialClosing
    {
      Id = Guid.NewGuid(),
      FinancialClosingId = sequentialId,
      ClosedAtUtc = closedAtUtc,
      TotalAmount = totalAmount,
      CreatedByUserId = createdByUserId
    };
  }
}
