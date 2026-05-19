using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.Commands.CreateReservation;
using Application.Reservations.Commands.UpdateReservation;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class UpdateReservationEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPut("reservations/{id:guid}", async (
      Guid id,
      ReservationRequest request,
      ICommandHandler<UpdateReservationCommand> handler,
      CancellationToken cancellationToken) =>
    {
      UpdateReservationCommand command = new(
        id,
        request.Name,
        request.Surname,
        request.Email,
        request.Phone,
        request.From,
        request.To,
        request.Note,
        request.GroupReservationId,
        request.SpotIds ?? [],
        request.Services?
          .Select(s => new ReservationServiceLine(s.ServiceId, s.Quantity, s.RecapSingleQuantity, s.RecapDayQuantity))
          .ToList() ?? [],
        request.Vehicles?
          .Select(v => new ReservationVehicleLine(v.Id, v.RegistrationNumber))
          .ToList() ?? [],
        DisplayName: request.DisplayName,
        Language: request.Language);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("UpdateReservation")
    .WithSummary("Replace the editable state of a reservation")
    .WithDescription("""
      Replaces the full editable state of a reservation: maker, period, note, group link,
      spot list, services, and inline vehicles.

      **Behavior:** the reservation must not be in `Cancelled` or `Completed` (those return
      `409`). The new spot list and new period are checked for availability, with the
      reservation itself excluded and overlap with the supplied group reservation allowed.
      Child collections diff by natural key - spots by `SpotId` (preserving
      `HasReturnedKeys`), services by `ServiceId` (updating quantities in place), and inline
      vehicles by `Vehicle.Id` (preserving any bill/service link set later via `/vehicles`).
      Vehicles supplied with `id == null` are added as new rows; existing vehicles whose id is
      absent from the payload are removed. A vehicle id that does not belong to this
      reservation is rejected with `400`. An optional `displayName` (max 100 chars) may be
      updated as a free-form, staff-only label for the reservation; omitting it clears the
      existing value.

      **Errors:** `400` invalid payload, vehicle id from another reservation. `404`
      reservation, spot, or service does not exist. `409` `Cancelled` / `Completed` edit, or
      one of the bound spots is unavailable in the new period.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
