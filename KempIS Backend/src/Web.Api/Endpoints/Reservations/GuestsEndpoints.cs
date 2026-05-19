using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.Guests;
using Application.Reservations.Guests.Commands.ClearGuestSignature;
using Application.Reservations.Guests.Commands.SetGuestSignature;
using Application.Reservations.Guests.Queries.GetGuestSignature;
using Domain.Common;
using Domain.Reservations.Guests;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class GuestsEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("guests")
      .WithTags(Tags.Reservations)
      .HasRole(Roles.Receptionist, Roles.Manager);

    group.MapGet(string.Empty, async (
      [FromQuery] DateOnly from,
      [FromQuery] DateOnly to,
      [FromQuery] string? search,
      IQueryHandler<GetGuestsQuery, List<GuestResponse>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<List<GuestResponse>> result =
        await handler.Handle(new GetGuestsQuery(from, to, search), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetGuests")
    .WithSummary("List guests within a bill date range")
    .WithDescription("""
      Returns guests whose linked `Bill`'s `[CheckInAt..CheckOutAt]` window overlaps the
      supplied `[from, to]` date range (inclusive on both ends). Guests with no linked bill
      are excluded.

      **Search:** the optional `search` query parameter performs a case-insensitive substring
      match across the guest's text fields (first/last name, document number, reason of stay,
      visa number, note, and address city/street/zip/house number). Whitespace is trimmed;
      an empty or whitespace-only value is ignored.

      **Errors:** `400` if `from` or `to` is missing, if `to < from`, or if `search` exceeds
      100 characters.
      """)
    .Produces<List<GuestResponse>>(StatusCodes.Status200OK)
    .ProducesValidationProblem();

    group.MapGet("{id:guid}", async (
      Guid id,
      IQueryHandler<GetGuestByIdQuery, GuestDetailResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<GuestDetailResponse> result = await handler.Handle(new GetGuestByIdQuery(id), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetGuestById")
    .WithSummary("Get a guest by id")
    .WithDescription("""
      Returns the full guest record by id: identity, document, address, reservation/bill links,
      stay state, signature presence (without raw bytes), and audit timestamps.

      **Errors:** `404` guest does not exist.
      """)
    .Produces<GuestDetailResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound);

    group.MapGet("in-camp-count", async (
      IQueryHandler<GetInCampGuestCountQuery, int> handler,
      CancellationToken cancellationToken) =>
    {
      Result<int> result = await handler.Handle(new GetInCampGuestCountQuery(), cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithName("GetInCampGuestCount")
    .WithSummary("Count guests currently in the camp")
    .WithDescription("""
      Returns the number of guests considered physically present: those with a non-null
      `CheckInAt` and a null `CheckOutAt`.
      """)
    .Produces<int>(StatusCodes.Status200OK);

    group.MapPost(string.Empty, async (
      CreateGuestRequest request,
      ICommandHandler<CreateGuestCommand, Guid> handler,
      CancellationToken cancellationToken) =>
    {
      CreateGuestCommand command = new(
        request.ReservationId,
        request.BillId,
        request.PaysRecreationFee,
        request.FirstName,
        request.LastName,
        request.NationalityId,
        request.DateOfBirth,
        request.DocumentType,
        request.DocumentNumber,
        request.Address,
        request.ReasonOfStay,
        request.StayDateRange,
        request.VisaNumber,
        request.Note,
        request.Scartation,
        request.CheckInAt,
        request.CheckOutAt,
        request.SignaturePngBase64);

      Result<Guid> result = await handler.Handle(command, cancellationToken);

      return result.Match(
        id => Results.Created($"/guests/{id}", id),
        CustomResults.Problem);
    })
    .WithName("CreateGuest")
    .WithSummary("Create a guest")
    .WithDescription("""
      Adds a new guest, optionally attached to a reservation. Includes identity, document,
      address, and an optional base64-encoded PNG signature.

      **Behavior:** a supplied signature is stored only if the guest's nationality requires one
      (i.e., non-Czech); otherwise it is silently dropped.

      **Errors:** `400` invalid payload (missing required fields, address validation,
      malformed PNG base64).
      """)
    .Produces<Guid>(StatusCodes.Status201Created)
    .ProducesValidationProblem();

    group.MapPut("{id:guid}", async (
      Guid id,
      UpdateGuestRequest request,
      ICommandHandler<UpdateGuestCommand> handler,
      CancellationToken cancellationToken) =>
    {
      UpdateGuestCommand command = new(
        id,
        request.ReservationId,
        request.BillId,
        request.PaysRecreationFee,
        request.FirstName,
        request.LastName,
        request.NationalityId,
        request.DateOfBirth,
        request.DocumentType,
        request.DocumentNumber,
        request.Address,
        request.ReasonOfStay,
        request.StayDateRange,
        request.VisaNumber,
        request.Note,
        request.Scartation,
        request.CheckInAt,
        request.CheckOutAt,
        request.SignaturePngBase64);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("UpdateGuest")
    .WithSummary("Update a guest")
    .WithDescription("""
      Edits an existing guest record. Same validation rules as `CreateGuest`; sending
      `reservationId: null` detaches the guest from any reservation. When a
      `signaturePngBase64` is supplied, it is stored only if the (possibly new) nationality
      requires a signature; otherwise the existing signature is cleared.

      **Errors:** `400` invalid payload. `404` guest does not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound);

    group.MapDelete("{id:guid}", async (
      Guid id,
      ICommandHandler<DeleteGuestCommand> handler,
      CancellationToken cancellationToken) =>
    {
      DeleteGuestCommand command = new(id);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("DeleteGuest")
    .WithSummary("Delete a guest")
    .WithDescription("""
      Permanently removes a guest record.

      **Errors:** `404` guest does not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound);

    group.MapPut("{id:guid}/signature", async (
      Guid id,
      SetGuestSignatureRequest request,
      ICommandHandler<SetGuestSignatureCommand> handler,
      CancellationToken cancellationToken) =>
    {
      SetGuestSignatureCommand command = new(id, request.SignaturePngBase64);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("SetGuestSignature")
    .WithSummary("Attach a signature to a guest")
    .WithDescription("""
      Stores a base64-encoded PNG signature on the guest and stamps the capture timestamp.

      **Behavior:** the signature is stored only when the guest's nationality requires one
      (i.e., non-Czech). For Czech guests the call succeeds but clears any existing signature
      instead of storing the new one.

      **Errors:** `400` invalid payload (malformed PNG base64). `404` guest does not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound);

    group.MapGet("{id:guid}/signature", async (
      Guid id,
      IQueryHandler<GetGuestSignatureQuery, GetGuestSignatureResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<GetGuestSignatureResponse> result = await handler.Handle(
        new GetGuestSignatureQuery(id), cancellationToken);

      return result.Match(
        response => Results.File(response.Content, "image/png"),
        CustomResults.Problem);
    })
    .WithName("GetGuestSignature")
    .WithSummary("Download a guest's signature image")
    .WithDescription("""
      Streams the stored signature as `image/png`.

      **Errors:** `404` guest does not exist or has no stored signature.
      """)
    .Produces(StatusCodes.Status200OK, contentType: "image/png")
    .ProducesProblem(StatusCodes.Status404NotFound);

    group.MapDelete("{id:guid}/signature", async (
      Guid id,
      ICommandHandler<ClearGuestSignatureCommand> handler,
      CancellationToken cancellationToken) =>
    {
      Result result = await handler.Handle(new ClearGuestSignatureCommand(id), cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithName("ClearGuestSignature")
    .WithSummary("Remove a guest's signature")
    .WithDescription("""
      Clears any stored signature and the capture timestamp on the guest.

      **Errors:** `404` guest does not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound);
  }
}

internal sealed record CreateGuestRequest(
  Guid? ReservationId,
  Guid? BillId,
  bool? PaysRecreationFee,
  string FirstName,
  string LastName,
  Guid NationalityId,
  DateOnly DateOfBirth,
  DocumentType? DocumentType,
  string? DocumentNumber,
  Address Address,
  string ReasonOfStay,
  DateRange? StayDateRange,
  string? VisaNumber,
  string? Note,
  DateOnly? Scartation,
  DateTime? CheckInAt,
  DateTime? CheckOutAt,
  string? SignaturePngBase64);

internal sealed record UpdateGuestRequest(
  Guid? ReservationId,
  Guid? BillId,
  bool? PaysRecreationFee,
  string FirstName,
  string LastName,
  Guid NationalityId,
  DateOnly DateOfBirth,
  DocumentType? DocumentType,
  string? DocumentNumber,
  Address Address,
  string ReasonOfStay,
  DateRange? StayDateRange,
  string? VisaNumber,
  string? Note,
  DateOnly? Scartation,
  DateTime? CheckInAt,
  DateTime? CheckOutAt,
  string? SignaturePngBase64);

internal sealed record SetGuestSignatureRequest(string SignaturePngBase64);
