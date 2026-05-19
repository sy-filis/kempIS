using Application.Abstractions.Messaging;

namespace Application.Finance.Bills.GetBillPdf;

public sealed record GetBillPdfQuery(Guid BillId) : IQuery<GetBillPdfResponse>;
