using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Invoices.GetInvoiceById;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class GetInvoiceByIdEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("invoices/{invoiceId:guid}", async (
      Guid invoiceId,
      IQueryHandler<GetInvoiceByIdQuery, GetInvoiceByIdResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<GetInvoiceByIdResponse> result = await handler.Handle(new GetInvoiceByIdQuery(invoiceId), cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Invoices)
    .WithName("GetInvoiceById")
    .WithSummary("Get an invoice by id")
    .WithDescription("""
      Returns the full invoice detail: header, status, payer, legal entity, the linked bill
      (if any), and all line items.

      **Errors:** `404` invoice does not exist.
      """)
    .Produces<GetInvoiceByIdResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager, Roles.Accountant);
  }
}
