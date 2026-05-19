using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.FinancialClosings.GetFinancialClosingReport;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class GetFinancialClosingReportEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("financial-closings/{id:guid}/pdf", async (
      Guid id,
      IQueryHandler<GetFinancialClosingReportQuery, GetFinancialClosingReportResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<GetFinancialClosingReportResponse> result =
        await handler.Handle(new GetFinancialClosingReportQuery(id), cancellationToken);

      return result.Match(
        response => Results.File(response.Content, response.ContentType, response.FileName),
        CustomResults.Problem);
    })
    .WithTags(Tags.Finance)
    .WithName("GetFinancialClosingReport")
    .WithSummary("Download a financial closing report")
    .WithDescription("""
      Streams the rendered PDF report for a financial closing - VAT recap by service group and
      the list of included bills. Lazy-renders the document on first request and caches it on
      the closing row for subsequent calls.

      **Side effects:** on first download the rendered PDF is persisted on the financial
      closing so future requests return immediately.

      **Errors:** `404` financial closing does not exist.
      """)
    .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager, Roles.Accountant);
  }
}
