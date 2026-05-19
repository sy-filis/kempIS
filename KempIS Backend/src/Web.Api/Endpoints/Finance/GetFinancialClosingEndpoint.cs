using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.FinancialClosings.GetFinancialClosing;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class GetFinancialClosingEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("financial-closings/{id:guid}", async (
      Guid id,
      IQueryHandler<GetFinancialClosingQuery, FinancialClosingDetailResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<FinancialClosingDetailResponse> result =
        await handler.Handle(new GetFinancialClosingQuery(id), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Finance)
    .WithName("GetFinancialClosing")
    .WithSummary("Get a financial closing detail")
    .WithDescription("""
      Returns header fields, the bills attached to the closing (with payer, payment type, and total),
      a cash/card payment split, a flat VAT recap by rate, and a VAT recap grouped by service type
      and rate. JSON counterpart of the PDF report at `/financial-closings/{id}/pdf`.

      **Limitations:** bill items without a linked service are excluded from the VAT recap - same
      behavior as the PDF report.

      **Errors:** `404` financial closing does not exist.
      """)
    .Produces<FinancialClosingDetailResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Accountant, Roles.Manager);
  }
}
