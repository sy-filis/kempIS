using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.FinancialClosings;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.FinancialClosings.GetFinancialClosing;

internal sealed class GetFinancialClosingQueryHandler(IApplicationDbContext db)
  : IQueryHandler<GetFinancialClosingQuery, FinancialClosingDetailResponse>
{
  // BillItems with ServiceId == null are dropped by the inner join to Services.
  private sealed record VatItemRow(
    string ServiceTypeName,
    decimal VatRatePercentage,
    uint RecapSingleQuantity,
    uint RecapDayQuantity,
    decimal UnitPrice);

  public async Task<Result<FinancialClosingDetailResponse>> Handle(
    GetFinancialClosingQuery query,
    CancellationToken cancellationToken)
  {
    FinancialClosing? closing = await db.FinancialClosings
      .AsNoTracking()
      .FirstOrDefaultAsync(c => c.Id == query.Id, cancellationToken);

    if (closing is null)
    {
      return Result.Failure<FinancialClosingDetailResponse>(
        FinancialClosingErrors.NotFound(query.Id));
    }

    List<FinancialClosingBillItem> bills = await db.Bills
      .AsNoTracking()
      .Where(b => b.FinancialClosingId == closing.Id)
      .OrderBy(b => b.IssuedAtUtc)
      .Select(b => new FinancialClosingBillItem(
        b.Id,
        b.Number,
        b.IssuedAtUtc,
        ((b.Payer.Name ?? string.Empty) + " " + (b.Payer.Surname ?? string.Empty)).Trim(),
        b.Payment.PaymentType,
        b.Payment.Amount,
        b.Kind,
        b.OriginalBillId))
      .ToListAsync(cancellationToken);

    List<VatItemRow> itemRows = await (
      from bi in db.BillItems.AsNoTracking()
      join b in db.Bills.AsNoTracking() on bi.BillId equals b.Id
      join s in db.Services.AsNoTracking() on bi.ServiceId equals s.Id
      join st in db.ServiceTypes.AsNoTracking() on s.ServiceTypeId equals st.Id
      where b.FinancialClosingId == closing.Id
      select new VatItemRow(st.Name, bi.VatRatePercentage, bi.RecapSingleQuantity, bi.RecapDayQuantity, bi.UnitPrice))
      .ToListAsync(cancellationToken);

    var vatRecapByServiceType = itemRows
      .GroupBy(r => new { r.ServiceTypeName, r.VatRatePercentage })
      .Select(g =>
      {
        decimal gross = Math.Round(
          g.Sum(r => (decimal)r.RecapSingleQuantity * r.RecapDayQuantity * r.UnitPrice),
          2, MidpointRounding.AwayFromZero);
        decimal net = Math.Round(gross / (1m + g.Key.VatRatePercentage / 100m), 2, MidpointRounding.AwayFromZero);
        decimal vat = gross - net;
        return new FinancialClosingVatRecapByServiceTypeRow(
          g.Key.ServiceTypeName, g.Key.VatRatePercentage, net, vat, gross);
      })
      .OrderBy(r => r.ServiceTypeName, StringComparer.Ordinal)
      .ThenBy(r => r.VatRatePercentage)
      .ToList();

    var vatRecap = vatRecapByServiceType
      .GroupBy(r => r.VatRatePercentage)
      .Select(g => new FinancialClosingVatRecapRow(
        g.Key,
        g.Sum(r => r.Net),
        g.Sum(r => r.Vat),
        g.Sum(r => r.Gross)))
      .OrderBy(r => r.VatRatePercentage)
      .ToList();

    decimal cash = bills
      .Where(b => b.PaymentType == Domain.Finance.Payments.PaymentType.Cash)
      .Sum(b => b.Total);
    decimal card = bills
      .Where(b => b.PaymentType == Domain.Finance.Payments.PaymentType.Card)
      .Sum(b => b.Total);
    FinancialClosingPaymentTotals paymentTotals = new(cash, card, cash + card);

    return Result.Success(new FinancialClosingDetailResponse(
      Id: closing.Id,
      FinancialClosingId: closing.FinancialClosingId,
      ClosedAtUtc: closing.ClosedAtUtc,
      CreatedByUserId: closing.CreatedByUserId,
      Bills: bills,
      PaymentTotals: paymentTotals,
      VatRecap: vatRecap,
      VatRecapByServiceType: vatRecapByServiceType));
  }
}
