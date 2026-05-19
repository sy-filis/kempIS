namespace Application.Finance.Bills.GetBillPdf;

public sealed record GetBillPdfResponse(byte[] Content, string ContentType, string FileName);
