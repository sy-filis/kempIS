using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Invoices.MarkInvoicePaid;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed record MarkInvoicePaidRequest(DateOnly PaidAt);

internal sealed class MarkInvoicePaidEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("invoices/{invoiceId:guid}/mark-paid", async (
      Guid invoiceId,
      MarkInvoicePaidRequest request,
      ICommandHandler<MarkInvoicePaidCommand> handler,
      CancellationToken cancellationToken) =>
    {
      var command = new MarkInvoicePaidCommand(invoiceId, request.PaidAt);
      Result result = await handler.Handle(command, cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Invoices)
    .WithName("MarkInvoicePaid")
    .WithSummary("Mark an invoice as paid")
    .WithDescription("""
      Promotes an issued invoice to `Paid` status and stamps it with the supplied payment
      timestamp. Once paid an invoice becomes eligible to be linked to a bill as a deduction.

      **Behavior:** the invoice must be in `Created` status. An `InvoiceMarkedPaidDomainEvent`
      is raised.

      **Errors:** `404` invoice does not exist. `409` invoice has not been issued yet
      (`Draft`) or is already paid.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Accountant);
  }
}
