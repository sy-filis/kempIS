using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Bills.ListBills;
using Domain.Finance.Bills;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed record ListBillsRequest(
  DateOnly? From, DateOnly? To, Guid? ReservationId, BillKind? Kind, Guid? FinancialClosingId, bool? Closed);

internal sealed class ListBillsEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("bills", async (
      [AsParameters] ListBillsRequest request,
      IQueryHandler<ListBillsQuery, IReadOnlyList<BillSummary>> handler,
      CancellationToken cancellationToken) =>
    {
      var query = new ListBillsQuery(request.From, request.To, request.ReservationId, request.Kind, request.FinancialClosingId, request.Closed);
      Result<IReadOnlyList<BillSummary>> result = await handler.Handle(query, cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Bills)
    .WithName("ListBills")
    .WithSummary("List bills with optional date and reservation filters")
    .WithDescription("""
      Returns bill summaries ordered by issue date (newest first). Each filter is optional and
      applied independently.

      **Behavior:** `from` and `to` filter on the bill's stay range - `from` is matched
      against `CheckOutAt >= from` (the bill's stay must end on or after `from`) and `to`
      against `CheckInAt <= to` (the bill's stay must start on or before `to`), so any bill
      whose stay overlaps the supplied window is returned. `kind` (`Regular` / `Repair`),
      `reservationId`, and `financialClosingId` apply equality filters when provided.
      `closed` filters on financial-closing membership: `true` returns only bills attached to
      a closing, `false` returns only unclosed (open) bills; omitting it leaves the closure
      state unfiltered. `closed` composes with `financialClosingId` - when both are sent,
      both apply. Results are not paginated.
      """)
    .Produces<IReadOnlyList<BillSummary>>(StatusCodes.Status200OK)
    .HasRole(Roles.Receptionist, Roles.Manager, Roles.Accountant);
  }
}
