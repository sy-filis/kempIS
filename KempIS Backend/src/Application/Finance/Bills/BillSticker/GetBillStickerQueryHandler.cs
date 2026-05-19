using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.Bills;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Bills.BillSticker;

internal sealed class GetBillStickerQueryHandler(
  IApplicationDbContext db,
  IBillStickerRenderer renderer)
  : IQueryHandler<GetBillStickerQuery, GetBillStickerResponse>
{
  public async Task<Result<GetBillStickerResponse>> Handle(
    GetBillStickerQuery query,
    CancellationToken cancellationToken)
  {
    Bill? bill = await db.Bills
      .AsNoTracking()
      .FirstOrDefaultAsync(b => b.Id == query.BillId, cancellationToken);

    if (bill is null)
    {
      return Result.Failure<GetBillStickerResponse>(BillErrors.NotFound(query.BillId));
    }

    Result<byte[]> rendered = await renderer.RenderAsync(bill, cancellationToken);
    if (rendered.IsFailure)
    {
      return Result.Failure<GetBillStickerResponse>(rendered.Error);
    }

    return Result.Success(new GetBillStickerResponse(
      rendered.Value,
      "application/pdf",
      $"bill-sticker-{bill.Number}.pdf"));
  }
}
