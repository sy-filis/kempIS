using Application.Abstractions.Messaging;
using Application.Finance.Bills.GetBillPdf;
using Application.Reservations.Commands.CancelReservationForGuest;
using Application.Reservations.Commands.OnlineCheckInForGuest;
using Application.Reservations.Queries.GetBillPdfForGuest;
using Application.Reservations.Queries.GetReservationForGuest;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class GetReservationForGuestEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("reservations/{id:guid}/guest", async (
      Guid id,
      [FromQuery]
      string secret,
      IQueryHandler<GetReservationForGuestQuery, ReservationForGuestResponse> handler,
      CancellationToken cancellationToken) =>
    {
      GetReservationForGuestQuery query = new(id, secret);

      Result<ReservationForGuestResponse> result = await handler.Handle(query, cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("GetReservationForGuest")
    .WithSummary("Get a reservation by id and guest secret")
    .WithDescription("""
      Public, anonymous endpoint used by guest-facing self-service links. Returns a
      guest-friendly view of the reservation: header, spot items with their group's printable
      service text, meals, and bills.

      **Behavior:** the `secret` query parameter must match the reservation's stored secret
      issued at creation time.

      **Errors:** `400` `secret` does not match. `404` reservation does not exist.
      """)
    .Produces<ReservationForGuestResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .AllowAnonymous();
  }
}

internal sealed class CancelReservationForGuestEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("reservations/{id:guid}/guest/cancel", async (
      Guid id,
      [FromQuery]
      string secret,
      ICommandHandler<CancelReservationForGuestCommand> handler,
      CancellationToken cancellationToken) =>
    {
      CancelReservationForGuestCommand command = new(id, secret);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("CancelReservationForGuest")
    .WithSummary("Cancel a reservation as a guest")
    .WithDescription("""
      Public, anonymous endpoint that lets a guest cancel their own reservation through a
      self-service link.

      **Behavior:** the `secret` query parameter must match the reservation's stored secret. A
      reservation already in `Cancelled` is rejected rather than acknowledged silently.

      **Errors:** `400` `secret` does not match or the reservation is already cancelled. `404`
      reservation does not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .AllowAnonymous();
  }
}

internal sealed class OnlineCheckInForGuestEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("reservations/{id:guid}/guest/check-in", async (
      Guid id,
      [FromQuery]
      string secret,
      OnlineCheckInRequest request,
      ICommandHandler<OnlineCheckInForGuestCommand> handler,
      CancellationToken cancellationToken) =>
    {
      OnlineCheckInForGuestCommand command = new(id, secret, request.Guests, request.Vehicles);
      Result result = await handler.Handle(command, cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("OnlineCheckInForGuest")
    .WithSummary("Submit online check-in details as a guest")
    .WithDescription("""
      Public, anonymous endpoint where the guest submits the full guest list and any vehicles
      ahead of arrival. Replaces any previously stored guests and vehicles for the reservation.

      **Behavior:** the `secret` query parameter must match. Per-guest document and visa rules
      are enforced: Czech minors under 15 may omit a document, otherwise a document type and
      number are required; the document type must be valid for the guest's nationality (EU vs
      non-EU); a visa number is required when the document is a passport for a visa-required
      nationality; `BIOMETRIKA` may only be used when the nationality qualifies for biometric
      exemption. Non-Czech guests must include a base64-encoded PNG signature.

      **Errors:** `400` invalid payload or any per-guest document/visa/signature rule fails.
      `404` reservation does not exist, or `secret` does not match (secret mismatches
      are reported as not-found). `409` online check-in has already been completed for
      this reservation.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .AllowAnonymous();
  }
}

internal sealed class GetBillPdfForGuestEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("reservations/{id:guid}/guest/bills/{billId:guid}/pdf", async (
      Guid id,
      Guid billId,
      [FromQuery]
      string secret,
      IQueryHandler<GetBillPdfForGuestQuery, GetBillPdfResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<GetBillPdfResponse> result = await handler.Handle(
        new GetBillPdfForGuestQuery(id, billId, secret), cancellationToken);

      return result.Match(
        response => Results.File(response.Content, response.ContentType, response.FileName),
        CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("GetBillPdfForGuest")
    .WithSummary("Download a bill PDF as a guest")
    .WithDescription("""
      Public, anonymous endpoint that streams the PDF for a bill linked to the guest's
      reservation. Lazy-renders the PDF the first time it is requested and caches it on the
      bill for subsequent calls.

      **Behavior:** the `secret` query parameter must match the reservation's stored secret,
      and the bill must belong to that reservation.

      **Side effects:** on first download the rendered PDF is persisted on the bill so future
      requests return immediately.

      **Errors:** `400` `secret` does not match. `404` reservation or bill does not exist (or
      the bill is not attached to this reservation).
      """)
    .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .AllowAnonymous();
  }
}

internal sealed record OnlineCheckInRequest(
  IReadOnlyList<OnlineCheckInGuest> Guests,
  IReadOnlyList<OnlineCheckInVehicle> Vehicles);
