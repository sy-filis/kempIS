using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Finance.Bills;
using Domain.Services.Services;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Finance.Queries.Stats.GetServiceRevenueStats;

internal sealed class GetServiceRevenueStatsQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetServiceRevenueStatsQuery, ServiceRevenueStatsResponse>
{
  public async Task<Result<ServiceRevenueStatsResponse>> Handle(
    GetServiceRevenueStatsQuery query,
    CancellationToken cancellationToken)
  {
    var fromUtc = query.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    var toUtc = query.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    // UnitPrice is gross; billed quantity = recapSingle × recapDay.
    List<AggregatedRow> aggregated = await (
      from bi in context.BillItems.AsNoTracking()
      join b in context.Bills on bi.BillId equals b.Id
      join s in context.Services on bi.ServiceId equals s.Id
      where b.Kind == BillKind.Regular
         && bi.ServiceId != null
         && b.IssuedAtUtc >= fromUtc
         && b.IssuedAtUtc < toUtc
      group new { bi.RecapSingleQuantity, bi.RecapDayQuantity, bi.UnitPrice }
        by new { s.ServiceGroup, s.Id, s.Name, s.IsActive, bi.VatRatePercentage } into g
      select new AggregatedRow(
          g.Key.ServiceGroup,
          g.Key.Id,
          g.Key.Name,
          g.Key.IsActive,
          g.Key.VatRatePercentage,
          g.Sum(x => (long)x.RecapSingleQuantity * x.RecapDayQuantity),
          g.Sum(x => (decimal)x.RecapSingleQuantity * x.RecapDayQuantity * x.UnitPrice))
    ).ToListAsync(cancellationToken);

    var groups = aggregated
      .GroupBy(a => a.ServiceGroup)
      .Select(g =>
      {
        var services = g
          .Select(a =>
          {
            decimal gross = Math.Round(a.Gross, 2, MidpointRounding.AwayFromZero);
            decimal net = Math.Round(gross / (1m + a.VatRatePercentage / 100m), 2, MidpointRounding.AwayFromZero);
            decimal vat = gross - net;
            return new ServiceRevenueRow(a.ServiceId, a.ServiceName, a.IsActive,
              a.VatRatePercentage, a.Quantity, net, vat, gross);
          })
          .OrderByDescending(r => r.Gross)
          .ThenBy(r => r.VatRatePercentage)
          .ToList();
        decimal groupNet = services.Sum(s => s.Net);
        decimal groupVat = services.Sum(s => s.Vat);
        decimal groupGross = services.Sum(s => s.Gross);
        return new ServiceRevenueGroup(g.Key.ToString(), groupNet, groupVat, groupGross, services);
      })
      .OrderByDescending(g => g.GroupGross)
      .ToList();

    decimal totalNet = groups.Sum(g => g.GroupNet);
    decimal totalVat = groups.Sum(g => g.GroupVat);
    decimal totalGross = groups.Sum(g => g.GroupGross);

    return new ServiceRevenueStatsResponse(query.From, query.To, totalNet, totalVat, totalGross, groups);
  }

  private readonly record struct AggregatedRow(
    ServiceGroup ServiceGroup,
    Guid ServiceId,
    string ServiceName,
    bool IsActive,
    decimal VatRatePercentage,
    long Quantity,
    decimal Gross);
}
