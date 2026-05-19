using Application.Abstractions.Messaging;

namespace Application.Finance.Queries.Stats.GetServiceRevenueStats;

public sealed record GetServiceRevenueStatsQuery(DateOnly From, DateOnly To)
  : IQuery<ServiceRevenueStatsResponse>;

public sealed record ServiceRevenueStatsResponse(
  DateOnly From,
  DateOnly To,
  decimal TotalNet,
  decimal TotalVat,
  decimal TotalGross,
  IReadOnlyList<ServiceRevenueGroup> Groups);

public sealed record ServiceRevenueGroup(
  string ServiceGroup,
  decimal GroupNet,
  decimal GroupVat,
  decimal GroupGross,
  IReadOnlyList<ServiceRevenueRow> Services);

public sealed record ServiceRevenueRow(
  Guid ServiceId,
  string ServiceName,
  bool IsActive,
  decimal VatRatePercentage,
  long Quantity,
  decimal Net,
  decimal Vat,
  decimal Gross);
