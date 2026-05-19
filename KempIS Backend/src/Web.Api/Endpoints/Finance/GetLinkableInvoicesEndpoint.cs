using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Bills.GetLinkableInvoices;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class GetLinkableInvoicesEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("reservations/{reservationId:guid}/linkable-invoices", async (
      Guid reservationId,
      IQueryHandler<GetLinkableInvoicesQuery, IReadOnlyList<LinkableInvoiceView>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<IReadOnlyList<LinkableInvoiceView>> result = await handler.Handle(new GetLinkableInvoicesQuery(reservationId), cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Bills)
    .WithName("GetLinkableInvoices")
    .WithSummary("List invoices that can still be linked to a reservation's bill")
    .WithDescription("""
      Returns invoices that are eligible to be deducted from a new bill on the supplied
      reservation: status `Paid` and not yet linked to any other bill. Each row carries the
      invoice's number, issue and paid timestamps, and gross total.
      """)
    .Produces<IReadOnlyList<LinkableInvoiceView>>(StatusCodes.Status200OK)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
