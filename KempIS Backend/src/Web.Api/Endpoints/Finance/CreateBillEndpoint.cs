using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Finance.Bills.CreateBill;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Finance;

internal sealed class CreateBillEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("bills", async (
      CreateBillCommand command,
      ICommandHandler<CreateBillCommand, CreateBillResponse> handler,
      CancellationToken cancellationToken) =>
    {
      command = command with
      {
        LinkedInvoiceIds = command.LinkedInvoiceIds ?? [],
        ExistingGuests = command.ExistingGuests ?? [],
        NewGuests = command.NewGuests ?? [],
        ReservationSpotItemIds = command.ReservationSpotItemIds ?? [],
        AccessCards = command.AccessCards ?? [],
        NewVehicles = command.NewVehicles ?? [],
        ExistingVehicleIds = command.ExistingVehicleIds ?? [],
      };

      Result<CreateBillResponse> result = await handler.Handle(command, cancellationToken);
      return result.Match(
        response => Results.Created($"/bills/{response.BillId}", response),
        CustomResults.Problem);
    })
    .WithTags(Tags.Bills)
    .WithName("CreateBill")
    .WithSummary("Create a bill (optionally linked to a reservation)")
    .WithDescription("""
      Issues a regular bill optionally linked to a reservation. Supports linking already-paid
      invoices as deductions, attaching a mix of existing and new guests to the bill, marking
      specific reservation spot items as paid by this bill via `reservationSpotItemIds`,
      issuing access cards inline via `accessCards` (each card is auto-linked to the new bill),
      creating fresh vehicle rows inline via `newVehicles` (each row auto-linked to the new
      bill and to the bill's reservation, if any), and linking already-registered vehicles via
      `existingVehicleIds` (their `billId` is set to the new bill; the existing `reservationId`
      and `serviceId` are left untouched).

      **Behavior:** every linked invoice must exist, be in `Paid` status, not already linked to
      another bill, and belong to the same reservation. Existing guests must not already be
      attached to another bill. Listed spot items must belong to the same reservation and must
      not already be linked to another bill. Linked invoice totals must not exceed the bill's
      items total - refunds require a separate document. Each inline access card UID must be
      globally unique. Each `existingVehicleIds` entry must reference an existing vehicle that
      is not already linked to another bill and whose `reservationId` matches the bill's
      reservation (both null is fine - walk-in vehicle on walk-in bill). The bill's number is
      generated server-side from the configured year-scoped sequence and a
      `BillCreatedDomainEvent` is raised. The bill, items, deductions, guests, spot-item links,
      access cards, and vehicles all commit (or roll back) atomically.

      **Errors:** `400` invalid payload (missing payer, no items, duplicate ids,
      duplicate access card UIDs in payload, duplicate vehicle ids in payload, walk-in bill
      with linked invoices or spot items, deductions exceed items total, invoice/reservation
      mismatch, spot item from another reservation, access card UID = 0, negative access card
      deposit, new vehicle registration empty/over 20 chars, new vehicle service id empty,
      existing vehicle from another reservation). `404` a linked invoice, supplied guest,
      supplied spot item, or supplied existing vehicle does not exist. `409` an invoice is not
      paid, is already linked to another bill, a guest is already attached elsewhere, a spot
      item is already linked to another bill, an access card UID is already issued, or an
      existing vehicle is already linked to another bill.
      """)
    .Produces<CreateBillResponse>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
