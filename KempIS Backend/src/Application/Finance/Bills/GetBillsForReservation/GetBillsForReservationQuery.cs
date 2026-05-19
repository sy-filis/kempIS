using Application.Abstractions.Messaging;
using Application.Finance.Bills.ListBills;

namespace Application.Finance.Bills.GetBillsForReservation;

public sealed record GetBillsForReservationQuery(Guid ReservationId)
  : IQuery<IReadOnlyList<BillSummary>>;
