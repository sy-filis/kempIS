using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Queries.Stats.GetRevenueByPaymentMethod;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Stats;

internal sealed class GetRevenueByPaymentMethodEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("stats/revenue/by-payment-method", async (
      [FromQuery] DateOnly from,
      [FromQuery] DateOnly to,
      IQueryHandler<GetRevenueByPaymentMethodQuery, RevenueByPaymentMethodResponse> handler,
      CancellationToken cancellationToken) =>
    {
      GetRevenueByPaymentMethodQuery query = new(from, to);
      Result<RevenueByPaymentMethodResponse> result = await handler.Handle(query, cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Stats)
    .WithName("GetRevenueByPaymentMethod")
    .WithSummary("Revenue grouped by payment method (cash, card)")
    .WithDescription("""
      Aggregates `Regular` bills whose `IssuedAtUtc` date falls inside `[from, to]`
      by `Bill.Payment.PaymentType`. Gross per row is derived from `BillItem`
      (`unitPrice × quantity × (1 + vat/100)`), so the total reconciles exactly
      with `/stats/services` for the same range.

      Both enum values (`Cash`, `Card`) are always emitted, even with zero, so
      charts have a stable shape. `sharePercent` is one decimal and sums to 100.0
      when any revenue exists. Repair bills are excluded.

      **Errors:** `400` when `to < from` or when the range spans more than 366 days.
      """)
    .Produces<RevenueByPaymentMethodResponse>(StatusCodes.Status200OK)
    .ProducesValidationProblem()
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
