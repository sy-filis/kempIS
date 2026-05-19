using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Reservations.Guests;
using Domain.Finance.Bills;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Bills.GetBillById;

internal sealed class GetBillByIdQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetBillByIdQuery, GetBillByIdResponse>
{
  public async Task<Result<GetBillByIdResponse>> Handle(
    GetBillByIdQuery query,
    CancellationToken cancellationToken)
  {
    GetBillByIdResponse? response = await context.Bills
      .AsNoTracking()
      .Where(b => b.Id == query.BillId)
      .Select(b => new GetBillByIdResponse(
        b.Id, b.Number, b.Kind, b.OriginalBillId, b.RepairReason, b.ReservationId, b.LanguageIdGuid,
        b.IssuedAtUtc, b.CheckInAt, b.CheckOutAt,
        new BillPayerView(b.Payer.Name, b.Payer.Surname, b.Payer.Address),
        b.LegalEntity == null
          ? null
          : new BillLegalEntityView(b.LegalEntity.Name, b.LegalEntity.Cin, b.LegalEntity.Tin, b.LegalEntity.Address),
        new BillPaymentView(b.Payment.PaymentType, b.Payment.Amount),
        context.BillItems.Where(i => i.BillId == b.Id)
          .Select(i => new BillItemView(i.Id, i.ServiceId, i.Quantity, i.UnitPrice, i.VatRatePercentage, i.RecapSingleQuantity, i.RecapDayQuantity))
          .ToList(),
        context.Invoices.Where(i => i.LinkedBillId == b.Id)
          .Select(i => new BillDeductionView(
            i.Id,
            i.Number,
            context.InvoiceItems.Where(item => item.InvoiceId == i.Id)
              .Sum(item => item.Quantity * item.UnitPrice)))
          .ToList(),
        context.Bills.Where(r => r.OriginalBillId == b.Id)
          .Select(r => new BillRepairSummary(r.Id, r.Number, r.IssuedAtUtc, r.Payment.Amount, r.RepairReason))
          .ToList(),
        context.Guests.Where(g => g.BillId == b.Id)
          .Select(g => new GuestDetailResponse(
            g.Id, g.ReservationId, g.BillId, g.PaysRecreationFee,
            g.FirstName, g.LastName, g.NationalityId, g.DateOfBirth,
            g.DocumentType, g.DocumentNumber, g.Address, g.ReasonOfStay,
            g.StayDateRange, g.VisaNumber, g.Note, g.Scartation,
            g.CheckInAt, g.CheckOutAt,
            g.SignaturePng != null, g.SignatureCapturedAtUtc,
            g.CreatedAt, g.UpdatedAt, g.ReportedAt))
          .ToList(),
        context.Vehicles.Where(v => v.BillId == b.Id)
          .Select(v => new BillVehicleView(v.Id, v.RegistrationNumber, v.ServiceId))
          .ToList(),
        context.ReservationSpotItems.Where(s => s.BillId == b.Id)
          .Select(s => new BillSpotItemView(s.Id, s.SpotId, s.HasGivenKey, s.HasReturnedKeys))
          .ToList(),
        context.AccessCards.Where(c => c.BillId == b.Id)
          .Select(c => new BillAccessCardView(c.Id, c.Uid, c.Deposit, c.IssuedAtUtc, c.Note))
          .ToList()))
      .FirstOrDefaultAsync(cancellationToken);

    if (response is null)
    {
      return Result.Failure<GetBillByIdResponse>(BillErrors.NotFound(query.BillId));
    }

    return Result.Success(response);
  }
}
