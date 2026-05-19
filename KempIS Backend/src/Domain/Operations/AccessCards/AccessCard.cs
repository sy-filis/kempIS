using SharedKernel;

namespace Domain.Operations.AccessCards;

public sealed class AccessCard : Entity
{
  public Guid Id { get; set; }

  public ulong Uid { get; set; }

  public Guid? BillId { get; set; }

  public decimal Deposit { get; set; }

  public DateOnly ValidUntil { get; set; }

  public DateTime IssuedAtUtc { get; set; }

  public string? Note { get; set; }
}
