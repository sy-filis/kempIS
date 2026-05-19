using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Invoices.DeleteInvoice;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class DeleteInvoiceEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapDelete("invoices/{invoiceId:guid}", async (
      Guid invoiceId,
      ICommandHandler<DeleteInvoiceCommand> handler,
      CancellationToken cancellationToken) =>
    {
      Result result = await handler.Handle(new DeleteInvoiceCommand(invoiceId), cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Invoices)
    .WithName("DeleteInvoice")
    .WithSummary("Delete a draft invoice")
    .WithDescription("""
      Removes a draft invoice and all of its line items. Once an invoice has been issued
      (`Created`) it is preserved as a permanent accounting record and cannot be deleted.

      **Errors:** `404` invoice does not exist. `409` invoice is no longer in `Draft` status.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
