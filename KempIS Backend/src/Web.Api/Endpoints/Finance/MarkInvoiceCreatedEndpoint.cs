using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Invoices.MarkInvoiceCreated;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed record MarkInvoiceCreatedRequest(string Number, DateOnly IssuedAt, DateOnly DueTo);

internal sealed class MarkInvoiceCreatedEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("invoices/{invoiceId:guid}/mark-created", async (
      Guid invoiceId,
      MarkInvoiceCreatedRequest request,
      ICommandHandler<MarkInvoiceCreatedCommand> handler,
      CancellationToken cancellationToken) =>
    {
      var command = new MarkInvoiceCreatedCommand(invoiceId, request.Number, request.IssuedAt, request.DueTo);
      Result result = await handler.Handle(command, cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Invoices)
    .WithName("MarkInvoiceCreated")
    .WithSummary("Mark an invoice as created (issued)")
    .WithDescription("""
      Promotes a draft invoice to `Created` status by stamping it with its formal external
      number, the issue date, and the payment due date.

      **Behavior:** the invoice must be in `Draft` status, and `number` must not be in use by
      any other invoice. `dueTo` is required and must be on or after `issuedAt`. An
      `InvoiceMarkedCreatedDomainEvent` is raised.

      **Errors:** `400` `number` is missing or longer than 50 characters, or `dueTo` is missing
      or precedes `issuedAt`. `404` invoice does not exist. `409` invoice is no longer in
      `Draft` status, or the supplied `number` is already taken by another invoice.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Accountant);
  }
}
