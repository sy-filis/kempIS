using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.Commands.CreateReservation;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class CreateReservationEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("reservations", async (
      ReservationRequest request,
      ICommandHandler<CreateReservationCommand, Guid> handler,
      CancellationToken cancellationToken) =>
    {
      CreateReservationCommand command = new(
        request.Name,
        request.Surname,
        request.Email,
        request.Phone,
        request.From,
        request.To,
        request.SpotIds ?? [],
        request.Note,
        request.GroupReservationId,
        request.Services?
          .Select(s => new ReservationServiceLine(s.ServiceId, s.Quantity, s.RecapSingleQuantity, s.RecapDayQuantity))
          .ToList() ?? [],
        request.Vehicles?
          .Select(v => new ReservationVehicleLine(v.Id, v.RegistrationNumber))
          .ToList() ?? [],
        DisplayName: request.DisplayName,
        Language: request.Language);

      Result<Guid> result = await handler.Handle(command, cancellationToken);

      return result.Match(
        id => Results.Created($"/reservations/{id}", id),
        CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("CreateReservation")
    .WithSummary("Create a reservation")
    .WithDescription("""
      Books a list of specific spots for a guest within a date range, optionally as part of a
      group reservation, and records the initial set of reservation services and inline
      vehicles.

      **Behavior:** every spot in `spotIds` must exist and be available for the entire
      `from`-`to` range; otherwise the call is rejected and no booking is made. Each entry in
      `services` references a `Service` from the catalog (rejected with `404` if missing); the
      `(reservation, service)` pair must be unique. Each entry in `vehicles` is created without
      a bill or service link (those are attached later via `PUT /vehicles/{id}`); the
      reservation's standalone `/vehicles` CRUD continues to operate alongside. An optional
      `displayName` (max 100 chars) may be supplied as a free-form, staff-only label for the
      reservation.

      **Errors:** `400` invalid payload (missing required fields, `to` not after `from`,
      malformed email, empty `spotIds`, duplicate `serviceId`s, empty registration plate).
      `404` one of the supplied spot or service ids does not exist. `409` one or more spots are
      already booked, out-of-order, or held by another group reservation in the requested
      period.
      """)
    .Produces<Guid>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}
