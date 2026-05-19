using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Bills.CreateRepairBill;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class CreateRepairBillEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("bills/repairs", async (
      CreateRepairBillCommand command,
      ICommandHandler<CreateRepairBillCommand, CreateRepairBillResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<CreateRepairBillResponse> result = await handler.Handle(command, cancellationToken);
      return result.Match(
        response => Results.Created($"/bills/{response.BillId}", response),
        CustomResults.Problem);
    })
    .WithTags(Tags.Bills)
    .WithName("CreateRepairBill")
    .WithSummary("Create a repair bill that adjusts an existing bill")
    .WithDescription("""
      Issues a repair bill that partially reverses items on a previously-issued regular bill.
      The repair bill copies the original's payer, legal entity, language, and stay dates,
      then charges only the supplied repair lines. The caller must supply a non-empty `reason`
      (max 500 chars) explaining why the repair is being issued; the reason is persisted on
      the bill, exposed via `GET /bills/{id}`, included in `BillRepairedDomainEvent`, and
      rendered on the printed repair PDF.

      **Behavior:** the original bill must exist and be of kind `Regular`. Each repair line
      must match an existing line on the original by `(ServiceId, UnitPrice, VatRatePercentage)`,
      and the requested `RecapSingleQuantity × RecapDayQuantity` may not exceed the matching
      original line's `RecapSingleQuantity × RecapDayQuantity` minus the sum of all prior
      repair consumption for that line. Both `BillCreatedDomainEvent` and `BillRepairedDomainEvent`
      are raised.

      **Errors:** `400` invalid payload (missing/oversize reason, zero recap quantities,
      out-of-range numbers), repair line not present on the original, or repair quantity
      exceeds the remaining cap. `404` original bill does not exist. `409` original bill is
      itself a repair bill.
      """)
    .Produces<CreateRepairBillResponse>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
