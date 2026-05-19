using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.Commands.CancelGroupReservation;
using Application.Reservations.Commands.CreateGroupReservation;
using Application.Reservations.Commands.SendGroupReservationInvitation;
using Application.Reservations.Commands.UpdateGroupReservation;
using Application.Reservations.Queries.GetGroupReservation;
using Application.Reservations.Queries.GetGroupReservations;
using Domain.Reservations.GroupReservations;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class CreateGroupReservationEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("group-reservations", async (
      CreateGroupReservationRequest request,
      ICommandHandler<CreateGroupReservationCommand, CreateGroupReservationResponse> handler,
      CancellationToken cancellationToken) =>
    {
      CreateGroupReservationCommand command = new(
        request.From,
        request.To,
        request.SpotIds,
        request.OrganizerName,
        request.OrganizerEmail,
        request.OrganizerPhone,
        request.Note,
        request.Language,
        DisplayName: request.DisplayName);

      Result<CreateGroupReservationResponse> result = await handler.Handle(command, cancellationToken);

      return result.Match(
        response => Results.Created($"/group-reservations/{response.Id}", response),
        CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("CreateGroupReservation")
    .WithSummary("Create a group reservation")
    .WithDescription("""
      Creates an organizer-led group reservation that holds a list of spots for a date range
      so individual members can later book against that hold via the public web reservation
      flow.

      **Behavior:** every spot must exist and be available for the entire period. The response
      includes a freshly generated `secret` that the organizer shares with members so they can
      claim spots inside the group, and a human-readable `number` of the form `GR-{year}/{seq}`
      assigned from a per-year sequence. An optional `displayName` (max 100 chars) may be
      supplied as a free-form, staff-only label for the group reservation.

      **Side effects:** dispatches one invitation email to the organizer in the requested
      `language` (currently `cs` or `en`); SMTP failures are logged at Warning and do not fail
      the request.

      **Errors:** `400` invalid payload (missing organizer details, `to` not after `from`,
      empty `spotIds`, missing or unsupported `language`). `404` one of the supplied spot ids
      does not exist. `409` one or more spots are unavailable in the requested period.
      """)
    .Produces<CreateGroupReservationResponse>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}

internal sealed class UpdateGroupReservationEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPut("group-reservations/{id:guid}", async (
      Guid id,
      UpdateGroupReservationRequest request,
      ICommandHandler<UpdateGroupReservationCommand> handler,
      CancellationToken cancellationToken) =>
    {
      UpdateGroupReservationCommand command = new(
        id,
        request.From,
        request.To,
        request.SpotIds,
        request.OrganizerName,
        request.OrganizerEmail,
        request.OrganizerPhone,
        request.Note,
        DisplayName: request.DisplayName);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("UpdateGroupReservation")
    .WithSummary("Update a group reservation")
    .WithDescription("""
      Replaces the editable fields of an existing group reservation: period, held spots,
      organizer details, and free-text note.

      **Behavior:** every supplied spot must exist. The held-spot list is replaced wholesale
      with the supplied `spotIds` (deduplicated). Existing member reservations attached to the
      group are not touched and may end up outside the new period or on spots no longer in the
      hold. No availability re-check is performed; the held-spot list is the organizer's
      declared intent. An optional `displayName` (max 100 chars) may be
      updated as a free-form, staff-only label for the group reservation; omitting it clears
      the existing value.

      **Errors:** `400` invalid payload (missing organizer details, `to` not after `from`,
      empty `spotIds`) or the group is in `Canceled` state. `404` group reservation does not
      exist, or one of the supplied spot ids does not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}

internal sealed class CancelGroupReservationEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("group-reservations/{id:guid}/cancel", async (
      Guid id,
      ICommandHandler<CancelGroupReservationCommand> handler,
      CancellationToken cancellationToken) =>
    {
      CancelGroupReservationCommand command = new(id);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("CancelGroupReservation")
    .WithSummary("Cancel a group reservation")
    .WithDescription("""
      Marks a group reservation as `Canceled`, releasing its hold so the spots become
      available for individual booking again.

      **Behavior:** existing member reservations attached to the group are not cancelled by
      this call.

      **Errors:** `400` the group reservation is already cancelled. `404` group reservation does
      not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}

internal sealed class SendGroupReservationInvitationEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("group-reservations/{id:guid}/send-invitation", async (
      Guid id,
      SendGroupReservationInvitationRequest request,
      ICommandHandler<SendGroupReservationInvitationCommand> handler,
      CancellationToken cancellationToken) =>
    {
      SendGroupReservationInvitationCommand command = new(id, request.Language);

      Result result = await handler.Handle(command, cancellationToken);

      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("SendGroupReservationInvitation")
    .WithSummary("Send a group reservation invitation email")
    .WithDescription("""
      Renders the `group-reservation-invitation` email template in the requested language and
      sends it to the organizer's address. The email contains the group id, secret, organizer
      name, period, and note so the organizer can forward a self-service link to members.

      **Side effects:** dispatches one email to the organizer.

      **Errors:** `400` invalid payload (missing or oversized `language`) or no template exists
      for the requested language. `404` group reservation does not exist.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}

internal sealed record SendGroupReservationInvitationRequest(string Language);

internal sealed class GetGroupReservationEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("group-reservations/{id:guid}", async (
      Guid id,
      IQueryHandler<GetGroupReservationQuery, GroupReservationResponse> handler,
      CancellationToken cancellationToken) =>
    {
      GetGroupReservationQuery query = new(id);

      Result<GroupReservationResponse> result = await handler.Handle(query, cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("GetGroupReservation")
    .WithSummary("Get a group reservation by id")
    .WithDescription("""
      Returns the full state of a group reservation: human-readable `number`, organizer details,
      period, current state, stored secret, and the list of held spot ids.

      **Errors:** `404` group reservation does not exist.
      """)
    .Produces<GroupReservationResponse>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}

internal sealed class GetGroupReservationsEndpoint : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("group-reservations", async (
      [FromQuery] DateOnly from,
      [FromQuery] DateOnly to,
      [FromQuery] GroupReservationState? state,
      IQueryHandler<GetGroupReservationsQuery, List<GroupReservationListItemResponse>> handler,
      CancellationToken cancellationToken) =>
    {
      GetGroupReservationsQuery query = new(from, to, state);

      Result<List<GroupReservationListItemResponse>> result = await handler.Handle(query, cancellationToken);

      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Reservations)
    .WithName("GetGroupReservations")
    .WithSummary("List group reservations whose period overlaps a date range")
    .WithDescription("""
      Returns group reservations whose held `period` overlaps the supplied `[from, to]` range,
      optionally narrowed to a single `state` (`Confirmed` or `Canceled`).

      **Behavior:** dates are inclusive on both ends. When `state` is omitted every state is
      returned. Results are ordered by period start ascending and then by creation time
      ascending. The response excludes the organizer-only `secret` and the free-text `note` -
      callers needing those should call `GET /group-reservations/{id}`.

      **Errors:** `400` invalid date format or unknown `state` value.
      """)
    .Produces<List<GroupReservationListItemResponse>>(StatusCodes.Status200OK)
    .ProducesValidationProblem()
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}

internal sealed record CreateGroupReservationRequest(
  DateOnly From,
  DateOnly To,
  IReadOnlyList<Guid> SpotIds,
  string OrganizerName,
  string OrganizerEmail,
  string OrganizerPhone,
  string? Note,
  string Language,
  string? DisplayName);

internal sealed record UpdateGroupReservationRequest(
  DateOnly From,
  DateOnly To,
  IReadOnlyList<Guid> SpotIds,
  string OrganizerName,
  string OrganizerEmail,
  string OrganizerPhone,
  string? Note,
  string? DisplayName);
