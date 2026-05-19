using Application.Abstractions.Messaging;
using Application.Finance.Bills.Shared;
using Domain.Finance.Payments;

namespace Application.Finance.Bills.CreateRepairBill;

public sealed record CreateRepairBillCommand(
  Guid OriginalBillId,
  PaymentType PaymentType,
  string Reason,
  IReadOnlyList<BillItemInput> Items)
  : ICommand<CreateRepairBillResponse>;

public sealed record CreateRepairBillResponse(Guid BillId, string Number);
