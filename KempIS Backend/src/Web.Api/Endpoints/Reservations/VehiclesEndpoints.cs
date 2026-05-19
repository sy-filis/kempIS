using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.Vehicles;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class VehiclesEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("vehicles")
      .WithTags(Tags.Reservations)
      .HasRole(Roles.Receptionist, Roles.Manager);

    group.MapGet(string.Empty, async (
      [FromQuery] DateOnly from,
      [FromQuery] DateOnly to,
      [FromQuery] string? search,
      IQueryHandler<GetVehiclesQuery, List<VehicleResponse>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<List<VehicleResponse>> result =
        await handler.Handle(new GetVehiclesQuery(from, to, search), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetVehicles")
    .WithSummary("List vehicles within a bill date range")
    .WithDescription("""
      Returns vehicles whose linked `Bill`'s `[CheckInAt..CheckOutAt]` window overlaps the
      supplied `[from, to]` date range (inclusive on both ends). Vehicles with no linked bill
      are excluded.

      **Search:** the optional `search` query parameter performs a case-insensitive substring
      match against `registrationNumber`. Whitespace is trimmed; an empty or whitespace-only
      value is ignored.

      **Errors:** `400` if `from` or `to` is missing, if `to < from`, or if `search` exceeds
      100 characters.
      """)
    .Produces<List<VehicleResponse>>(StatusCodes.Status200OK)
    .ProducesValidationProblem();

    group.MapPost(string.Empty, async (
      VehicleRequest request,
      ICommandHandler<CreateVehicleCommand, Guid> handler,
      CancellationToken cancellationToken) =>
    {
      CreateVehicleCommand command = new(
        request.ReservationId,
        request.BillId,
        request.ServiceId,
        request.RegistrationNumber);

      Result<Guid> result = await handler.Handle(command, cancellationToken);

      return result.Match(
        id => Results.Created($"/vehicles/{id}", id),
        CustomResults.Problem);
    })
    .WithName("CreateVehicle")
    .WithSummary("Create a vehicle")
    .WithDescription("""
      Registers a vehicle. All three foreign keys are optional and represent independent
      concerns: `reservationId` ties the vehicle to a guest's stay, `billId` ties it to a
      charge, and `serviceId` records which parking/fee service the vehicle is consuming.
      A vehicle with all three null is a plate logged for the parking lot with no billing
      yet - it may be linked later via PUT or via inline creation on `POST /bills`.

      **Errors:** `400` invalid payload (missing registration number, registration over
      20 chars, any supplied id equal to `Guid.Empty`).
      """)
    .Produces<Guid>(StatusCodes.Status201Created)
    .ProducesValidationProblem();

    group.MapPut("{id:guid}", async (
      Guid id,
      VehicleRequest request,
      ICommandHandler<UpdateVehicleCommand> handler,
      CancellationToken cancellationToken) =>
    {
      UpdateVehicleCommand command = new(
        id,
        request.ReservationId,
        request.BillId,
        request.ServiceId,
        request.RegistrationNumber);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("UpdateVehicle")
    .WithSummary("Update a vehicle")
    .WithDescription("""
      Edits a vehicle's reservation, bill, service, and registration number. All three
      foreign keys may be null - see POST /api/vehicles for the meaning of each missing key.

      **Errors:** `400` invalid payload (missing registration number, registration over
      20 chars, any supplied id equal to `Guid.Empty`). `404` vehicle does not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound);

    group.MapDelete("{id:guid}", async (
      Guid id,
      ICommandHandler<DeleteVehicleCommand> handler,
      CancellationToken cancellationToken) =>
    {
      DeleteVehicleCommand command = new(id);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("DeleteVehicle")
    .WithSummary("Delete a vehicle")
    .WithDescription("""
      Permanently removes a vehicle.

      **Errors:** `404` vehicle does not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound);
  }
}

internal sealed record VehicleRequest(
  Guid? ReservationId,
  Guid? BillId,
  Guid? ServiceId,
  string RegistrationNumber);
