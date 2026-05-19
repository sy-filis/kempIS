using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Invoices.Shared;
using Application.Finance.Invoices.UpdateInvoice;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed record UpdateInvoiceRequest(
  InvoicePayerInput? Payer,
  InvoiceLegalEntityInput? LegalEntity,
  string Email,
  string PhoneNumber,
  IReadOnlyList<InvoiceItemInput> Items,
  DateOnly? DueTo = null);

internal sealed class UpdateInvoiceEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPut("invoices/{invoiceId:guid}", async (
      Guid invoiceId,
      UpdateInvoiceRequest request,
      ICommandHandler<UpdateInvoiceCommand> handler,
      CancellationToken cancellationToken) =>
    {
      var command = new UpdateInvoiceCommand(
        invoiceId, request.Payer, request.LegalEntity, request.Email, request.PhoneNumber, request.Items, request.DueTo);
      Result result = await handler.Handle(command, cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Invoices)
    .WithName("UpdateInvoice")
    .WithSummary("Update a draft invoice")
    .WithDescription("""
      Replaces the payer-or-legal-entity, contact details, and items of a draft invoice. The
      existing items are removed and rewritten from the request payload.

      **Behavior:** the invoice must be in `Draft` status; once issued (`Created`) or paid it
      is immutable. Exactly one of `payer` or `legalEntity` must be supplied; `email` and
      `phoneNumber` are required.

      **Errors:** `400` invalid payload (both/neither party, missing contact fields, malformed
      email, missing items, out-of-range item numbers). `404` invoice does not exist. `409`
      invoice is no longer in `Draft` status.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Receptionist, Roles.Accountant);
  }
}
