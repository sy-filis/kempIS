using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Finance.Bills;
using Application.Finance.Bills.GetBillPdf;
using Domain.Finance.Bills;
using Domain.Reservations;
using Domain.Reservations.Reservations;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Queries.GetBillPdfForGuest;

internal sealed class GetBillPdfForGuestQueryHandler(
  IApplicationDbContext db,
  IBillDocumentRenderer renderer,
  IDateTimeProvider dateTimeProvider)
  : IQueryHandler<GetBillPdfForGuestQuery, GetBillPdfResponse>
{
  public async Task<Result<GetBillPdfResponse>> Handle(
    GetBillPdfForGuestQuery query,
    CancellationToken cancellationToken)
  {
    Reservation? reservation = await db.Reservations
      .FirstOrDefaultAsync(r => r.Id == query.ReservationId, cancellationToken);

    if (reservation is null)
    {
      return Result.Failure<GetBillPdfResponse>(ReservationErrors.NotFound(query.ReservationId));
    }

    if (!string.Equals(reservation.Secret, query.Secret, StringComparison.Ordinal))
    {
      return Result.Failure<GetBillPdfResponse>(ReservationErrors.SecretInvalid);
    }

    Bill? bill = await db.Bills
      .FirstOrDefaultAsync(b => b.Id == query.BillId && b.ReservationId == query.ReservationId, cancellationToken);

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
