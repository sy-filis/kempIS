using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Invoices.GetInvoicesForReservation;
using Application.Finance.Invoices.ListInvoices;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class GetInvoicesForReservationEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("reservations/{reservationId:guid}/invoices", async (
      Guid reservationId,
      IQueryHandler<GetInvoicesForReservationQuery, IReadOnlyList<InvoiceSummary>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<IReadOnlyList<InvoiceSummary>> result = await handler.Handle(new GetInvoicesForReservationQuery(reservationId), cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Invoices)
    .WithName("GetInvoicesForReservation")
    .WithSummary("List invoices attached to a reservation")
    .WithDescription("""
      Returns every invoice - in any status - whose `ReservationId` matches the supplied
      reservation, ordered by issue timestamp (newest first), each augmented with the gross
      total of its line items. An unknown reservation id returns an empty list.
      """)
    .Produces<IReadOnlyList<InvoiceSummary>>(StatusCodes.Status200OK)
    .HasRole(Roles.Receptionist, Roles.Manager, Roles.Accountant);
  }
}
