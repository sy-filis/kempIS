using Application.Abstractions.Messaging;
using Application.Finance.Bills.GetBillPdf;

namespace Application.Reservations.Queries.GetBillPdfForGuest;

public sealed record GetBillPdfForGuestQuery(Guid ReservationId, Guid BillId, string Secret)
  : IQuery<GetBillPdfResponse>;
