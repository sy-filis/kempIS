using Application.Abstractions.Messaging;

namespace Application.Finance.Bills.BillSticker;

public sealed record GetBillStickerQuery(Guid BillId) : IQuery<GetBillStickerResponse>;
