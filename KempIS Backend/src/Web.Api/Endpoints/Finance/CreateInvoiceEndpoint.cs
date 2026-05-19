using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Invoices.CreateInvoice;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class CreateInvoiceEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("invoices", async (
      CreateInvoiceCommand command,
      ICommandHandler<CreateInvoiceCommand, CreateInvoiceResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<CreateInvoiceResponse> result = await handler.Handle(command, cancellationToken);
      return result.Match(
        response => Results.Created($"/invoices/{response.InvoiceId}", response),
        CustomResults.Problem);
    })
    .WithTags(Tags.Invoices)
    .WithName("CreateInvoice")
    .WithSummary("Create an invoice")
    .WithDescription("""
      Creates a new invoice in `Draft` status against a reservation. The invoice is identified
      only by id at this stage - its formal number is assigned later via `MarkInvoiceCreated`.

      **Behavior:** exactly one of `payer` or `legalEntity` must be supplied (not both, not
      neither). `email`, `phoneNumber`, and at least one line item are required. The issued
      timestamp is stamped server-side. An `InvoiceCreatedDomainEvent` is raised.

      **Errors:** `400` invalid payload (missing reservation, both/neither party, missing
      contact fields, malformed email, missing items, out-of-range item numbers).
      """)
    .Produces<CreateInvoiceResponse>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
