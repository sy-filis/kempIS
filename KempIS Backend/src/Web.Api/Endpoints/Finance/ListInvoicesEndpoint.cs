using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Invoices.ListInvoices;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed record ListInvoicesRequest(DateTime? From, DateTime? To, Guid? ReservationId, InvoiceStateFilter? State);

internal sealed class ListInvoicesEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("invoices", async (
      [AsParameters] ListInvoicesRequest request,
      IQueryHandler<ListInvoicesQuery, IReadOnlyList<InvoiceSummary>> handler,
      CancellationToken cancellationToken) =>
    {
      var query = new ListInvoicesQuery(request.From, request.To, request.ReservationId, request.State);
      Result<IReadOnlyList<InvoiceSummary>> result = await handler.Handle(query, cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Invoices)
    .WithName("ListInvoices")
    .WithSummary("List invoices")
    .WithDescription("""
      Returns invoice summaries ordered by issue timestamp (newest first), each augmented with
      the reservation overview (id, number, period) and the gross total of its line items.

      **Behavior:** `from` and `to` filter on the **reservation period** (overlap with
      `[from, to]`), not on the invoice's issue timestamp. `reservationId` applies equality.
      `state` accepts `Draft`, `Created`, `Paid`, or the virtual `AfterDue` (matches invoices
      with `Status = Created` whose `dueToUtc` has passed). Results are not paginated.
      """)
    .Produces<IReadOnlyList<InvoiceSummary>>(StatusCodes.Status200OK)
    .HasRole(Roles.Receptionist, Roles.Manager, Roles.Accountant);
  }
}
