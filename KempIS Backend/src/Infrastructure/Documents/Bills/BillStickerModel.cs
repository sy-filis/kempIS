namespace Infrastructure.Documents.Bills;

public sealed record BillStickerModel(string QrPngBase64, DateOnly CheckOutAt);
