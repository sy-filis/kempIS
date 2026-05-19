using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Queries.Stats.GetServiceRevenueStats;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Stats;

internal sealed class GetServiceRevenueStatsEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("stats/services", async (
      [FromQuery] DateOnly from,
      [FromQuery] DateOnly to,
      IQueryHandler<GetServiceRevenueStatsQuery, ServiceRevenueStatsResponse> handler,
      CancellationToken cancellationToken) =>
    {
      GetServiceRevenueStatsQuery query = new(from, to);
      Result<ServiceRevenueStatsResponse> result = await handler.Handle(query, cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Stats)
    .WithName("GetServiceRevenueStats")
    .WithSummary("Service revenue grouped by service group and service")
    .WithDescription("""
      Aggregates `BillItem` rows from `Regular` bills whose `IssuedAtUtc` date falls
      inside `[from, to]`. Result is nested: each `ServiceGroup` contains its
      services, sorted by gross desc. Group/global totals equal the sum of their
      service rows.

      A service with two different `VatRatePercentage` values across the period
      emits one row per `(serviceId, vatRatePercentage)` pair, preserving
      `BillItem`-level VAT history. Inactive services with sales are included
      (`isActive` exposed on each row); bill items without a `serviceId` and
      repair bills are excluded.

      **Errors:** `400` when `to < from` or when the range spans more than 366 days.
      """)
    .Produces<ServiceRevenueStatsResponse>(StatusCodes.Status200OK)
    .ProducesValidationProblem()
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
