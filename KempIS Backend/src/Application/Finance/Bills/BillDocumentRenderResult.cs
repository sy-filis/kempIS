namespace Application.Finance.Bills;

public sealed record BillDocumentRenderResult(byte[] Content, string ContentType, string LanguageCode);
