using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.Bills;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Bills.GetBillPdf;

internal sealed class GetBillPdfQueryHandler(
  IApplicationDbContext db,
  IBillDocumentRenderer renderer,
  IDateTimeProvider dateTimeProvider)
  : IQueryHandler<GetBillPdfQuery, GetBillPdfResponse>
{
  public async Task<Result<GetBillPdfResponse>> Handle(
    GetBillPdfQuery query,
    CancellationToken cancellationToken)
  {
    Bill? bill = await db.Bills
      .FirstOrDefaultAsync(b => b.Id == query.BillId, cancellationToken);

    if (bill is null)
    {
      return Result.Failure<GetBillPdfResponse>(BillErrors.NotFound(query.BillId));
    }

    if (bill.DocumentContent is not null)
    {
      return Result.Success(new GetBillPdfResponse(
        bill.DocumentContent,
        "application/pdf",
        $"bill-{bill.Number}.pdf"));
    }

    Result<BillDocumentRenderResult> rendered = await renderer.RenderAsync(query.BillId, cancellationToken);
    if (rendered.IsFailure)
    {
      return Result.Failure<GetBillPdfResponse>(rendered.Error);
    }

    bill.DocumentContent = rendered.Value.Content;
    bill.DocumentGeneratedAtUtc = dateTimeProvider.UtcNow;

    await db.SaveChangesAsync(cancellationToken);

    return Result.Success(new GetBillPdfResponse(
      rendered.Value.Content,
      "application/pdf",
      $"bill-{bill.Number}.pdf"));
  }
}
