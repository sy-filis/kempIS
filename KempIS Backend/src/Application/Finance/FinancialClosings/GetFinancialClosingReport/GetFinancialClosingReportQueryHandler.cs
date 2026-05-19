using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.FinancialClosings;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.FinancialClosings.GetFinancialClosingReport;

internal sealed class GetFinancialClosingReportQueryHandler(
  IApplicationDbContext db,
  IFinancialClosingReportRenderer renderer,
  IDateTimeProvider dateTimeProvider)
  : IQueryHandler<GetFinancialClosingReportQuery, GetFinancialClosingReportResponse>
{
  public async Task<Result<GetFinancialClosingReportResponse>> Handle(
    GetFinancialClosingReportQuery query,
    CancellationToken cancellationToken)
  {
    FinancialClosing? closing = await db.FinancialClosings
      .FirstOrDefaultAsync(c => c.Id == query.FinancialClosingId, cancellationToken);

    if (closing is null)
    {
      return Result.Failure<GetFinancialClosingReportResponse>(
        FinancialClosingErrors.NotFound(query.FinancialClosingId));
    }

    string fileName = $"financial-closing-{closing.FinancialClosingId}.pdf";

    if (closing.DocumentContent is not null)
    {
      return Result.Success(new GetFinancialClosingReportResponse(
        closing.DocumentContent,
        "application/pdf",
        fileName));
    }

    Result<byte[]> rendered = await renderer.RenderAsync(closing, cancellationToken);
    if (rendered.IsFailure)
    {
      return Result.Failure<GetFinancialClosingReportResponse>(rendered.Error);
    }

    closing.DocumentContent = rendered.Value;
    closing.DocumentGeneratedAtUtc = dateTimeProvider.UtcNow;

    await db.SaveChangesAsync(cancellationToken);

    return Result.Success(new GetFinancialClosingReportResponse(
      rendered.Value,
      "application/pdf",
      fileName));
  }
}
