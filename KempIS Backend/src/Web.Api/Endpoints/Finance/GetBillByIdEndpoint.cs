using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Bills.GetBillById;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class GetBillByIdEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("bills/{billId:guid}", async (
      Guid billId,
      IQueryHandler<GetBillByIdQuery, GetBillByIdResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<GetBillByIdResponse> result = await handler.Handle(new GetBillByIdQuery(billId), cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Bills)
    .WithName("GetBillById")
    .WithSummary("Get a bill by id")
    .WithDescription("""
      Returns the full bill detail: header, payer, legal entity, payment, items, deductions
      against linked invoices, the list of repair bills issued against it, the guests
      attached to it, and the vehicles and reservation spot items linked to it.

      **Errors:** `404` bill does not exist.
      """)
    .Produces<GetBillByIdResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager, Roles.Accountant);
  }
}
