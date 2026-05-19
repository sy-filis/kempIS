namespace Application.Finance.Bills.BillSticker;

public sealed record GetBillStickerResponse(byte[] Content, string ContentType, string FileName);
