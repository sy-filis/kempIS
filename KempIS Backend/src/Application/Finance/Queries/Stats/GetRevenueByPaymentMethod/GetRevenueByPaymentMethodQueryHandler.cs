using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.Bills;
using Domain.Finance.Payments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Queries.Stats.GetRevenueByPaymentMethod;

internal sealed class GetRevenueByPaymentMethodQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetRevenueByPaymentMethodQuery, RevenueByPaymentMethodResponse>
{
  public async Task<Result<RevenueByPaymentMethodResponse>> Handle(
    GetRevenueByPaymentMethodQuery query,
    CancellationToken cancellationToken)
  {
    var fromUtc = query.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    var toUtc = query.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    List<RawRow> raw = await (
      from bi in context.BillItems.AsNoTracking()
      join b in context.Bills on bi.BillId equals b.Id
      where b.Kind == BillKind.Regular
         && b.IssuedAtUtc >= fromUtc
         && b.IssuedAtUtc < toUtc
      select new RawRow(
          b.Payment.PaymentType,
          b.Id,
          bi.RecapSingleQuantity,
          bi.RecapDayQuantity,
          bi.UnitPrice)
    ).ToListAsync(cancellationToken);

    Dictionary<PaymentType, (decimal Gross, HashSet<Guid> Bills)> byMethod = new();
    foreach (PaymentType pt in Enum.GetValues<PaymentType>())
    {
      byMethod[pt] = (0m, new HashSet<Guid>());
    }

    foreach (RawRow r in raw)
    {
      // UnitPrice is gross; billed quantity = recapSingle × recapDay.
      decimal rowGross = (decimal)r.RecapSingleQuantity * r.RecapDayQuantity * r.UnitPrice;
      (decimal Gross, HashSet<Guid> Bills) acc = byMethod[r.PaymentType];
      acc.Bills.Add(r.BillId);
      byMethod[r.PaymentType] = (acc.Gross + rowGross, acc.Bills);
    }

    // Round per row before computing SharePercent so totals match rows.Sum exactly.
    var rows = byMethod
      .Select(kv => new RevenueByPaymentMethodRow(
        kv.Key.ToString(),
        kv.Value.Bills.Count,
        Math.Round(kv.Value.Gross, 2, MidpointRounding.AwayFromZero),
        0m))
      .OrderByDescending(r => r.Gross)
      .ToList();

    decimal totalGross = rows.Sum(r => r.Gross);
    int totalBillCount = rows.Sum(r => r.BillCount);

    var rowsWithShare = rows
      .Select(r => r with
      {
        SharePercent = totalGross == 0m
          ? 0m
          : Math.Round(100m * r.Gross / totalGross, 1, MidpointRounding.AwayFromZero),
      })
      .ToList();

    return new RevenueByPaymentMethodResponse(query.From, query.To, totalBillCount, totalGross, rowsWithShare);
  }

  private readonly record struct RawRow(
    PaymentType PaymentType,
    Guid BillId,
    uint RecapSingleQuantity,
    uint RecapDayQuantity,
    decimal UnitPrice);
}
