using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Bills.GetBillPdf;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class GetBillPdfEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("bills/{billId:guid}/pdf", async (
      Guid billId,
      IQueryHandler<GetBillPdfQuery, GetBillPdfResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<GetBillPdfResponse> result = await handler.Handle(new GetBillPdfQuery(billId), cancellationToken);

      return result.Match(
        response => Results.File(response.Content, response.ContentType, response.FileName),
        CustomResults.Problem);
    })
    .WithTags(Tags.Bills)
    .WithName("GetBillPdf")
    .WithSummary("Download a bill as PDF")
    .WithDescription("""
      Streams the rendered PDF for a bill. Lazy-renders the document the first time it is
      requested and caches it on the bill row for subsequent calls.

      **Side effects:** on first download the rendered PDF is persisted on the bill so future
      requests return immediately.

      **Errors:** `404` bill does not exist.
      """)
    .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager, Roles.Accountant);
  }
}
