using SharedKernel;

namespace Domain.Finance.Bills;

public sealed record BillRepairedDomainEvent(
  Guid OriginalBillId,
  Guid RepairBillId,
  string Reason) : IDomainEvent;
