using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Bills.BillSticker;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class GetBillStickerEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("bills/{billId:guid}/sticker.pdf", async (
      Guid billId,
      IQueryHandler<GetBillStickerQuery, GetBillStickerResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<GetBillStickerResponse> result =
        await handler.Handle(new GetBillStickerQuery(billId), cancellationToken);

      return result.Match(
        response => Results.File(response.Content, response.ContentType, response.FileName),
        CustomResults.Problem);
    })
    .WithTags(Tags.Bills)
    .WithName("GetBillSticker")
    .WithSummary("Download a bill registration sticker as PDF")
    .WithDescription("""
      Renders a small PDF sticker (62 mm x 19 mm) that encodes the bill id and check-out date
      as a QR code, intended to be printed and attached to physical guest registration cards.

      **Errors:** `404` bill does not exist.
      """)
    .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager, Roles.Accountant);
  }
}
