using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Operations.AccessCards;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Operations;

internal sealed class AccessCardsEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapPost("access-cards", async (
      IssueAccessCardRequest request,
      ICommandHandler<IssueAccessCardCommand, AccessCardResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<AccessCardResponse> result = await handler.Handle(
        new IssueAccessCardCommand(request.BillId, request.Uid, request.Deposit, request.ValidUntil, request.Note),
        cancellationToken);
      return result.Match(
        card => Results.Created($"/access-cards/{card.Id}", card),
        CustomResults.Problem);
    })
    .WithTags(Tags.Operations)
    .WithName("IssueAccessCard")
    .WithSummary("Issue an access card")
    .WithDescription("""
      Registers an access card with the supplied UID and deposit, optionally linked to a
      bill and tagged with a free-form note. The issued-at timestamp is stamped server-side
      at the current UTC time.

      **Behavior:** when `billId` is supplied, the bill must exist. The card UID must be
      globally unique - issuing a second card with a UID already in use is rejected so a
      freshly issued card cannot collide with one still in circulation.

      The `validUntil` calendar date is sent to the gate system as the card's expiry; the
      card stops opening doors at end-of-day Europe/Prague on that date.

      **Errors:** `400` validation failure (UID not greater than zero, negative deposit,
      empty bill id when supplied, missing `validUntil`). `404` no bill exists with the
      supplied id. `409` the supplied UID is already in use by another access card.
      """)
    .Produces<AccessCardResponse>(StatusCodes.Status201Created)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .HasRole(Roles.Receptionist, Roles.Manager);

    app.MapGet("access-cards", async (
      IQueryHandler<ListAccessCardsQuery, IReadOnlyList<AccessCardResponse>> handler,
      CancellationToken cancellationToken) =>
    {
      Result<IReadOnlyList<AccessCardResponse>> result = await handler.Handle(
        new ListAccessCardsQuery(),
        cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Operations)
    .WithName("ListAccessCards")
    .WithSummary("List all access cards currently in circulation")
    .WithDescription("""
      Returns every access card currently issued, ordered by issuance time newest-first.
      Each card carries a basic bill summary when linked to one. Cards that have been
      returned are deleted on return and so are not present.
      """)
    .Produces<IReadOnlyList<AccessCardResponse>>(StatusCodes.Status200OK)
    .HasRole(Roles.Receptionist, Roles.Manager);

    app.MapPatch("access-cards/{id:guid}", async (
      Guid id,
      UpdateAccessCardRequest request,
      ICommandHandler<UpdateAccessCardCommand, AccessCardResponse> handler,
      CancellationToken cancellationToken) =>
    {
      Result<AccessCardResponse> result = await handler.Handle(
        new UpdateAccessCardCommand(id, request.ValidUntil, request.Note),
        cancellationToken);
      return result.Match(Results.Ok, CustomResults.Problem);
    })
    .WithTags(Tags.Operations)
    .WithName("UpdateAccessCard")
    .WithSummary("Update an access card's validity and note")
    .WithDescription("""
      Updates the calendar expiry (`validUntil`) and operator-facing `note` of an existing
      access card. The card's UID, deposit, issued-at timestamp, and bill linkage stay
      unchanged. The new `validUntil` is sent to the gate system so the change takes effect
      at the door; gate failures are logged and swallowed.

      **Errors:** `400` validation failure (missing `validUntil`). `404` no access card
      exists with the supplied id.
      """)
    .Produces<AccessCardResponse>(StatusCodes.Status200OK)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager);

    app.MapDelete("access-cards/{id:guid}", async (
      Guid id,
      ICommandHandler<ReturnAccessCardCommand> handler,
      CancellationToken cancellationToken) =>
    {
      Result result = await handler.Handle(new ReturnAccessCardCommand(id), cancellationToken);
      return result.Match(Results.NoContent, CustomResults.Problem);
    })
    .WithTags(Tags.Operations)
    .WithName("ReturnAccessCard")
    .WithSummary("Return an access card and remove it from circulation")
    .WithDescription("""
      Records the return of an access card by deleting its row, freeing the UID for reuse
      on a future issuance. Use this when the guest physically hands the card back.

      **Behavior:** refunding the deposit recorded at issuance is a desk-side action; this
      endpoint only records the return and does not move money.

      **Errors:** `404` no access card exists with the supplied id.
      """)
    .Produces(StatusCodes.Status204NoContent)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .HasRole(Roles.Receptionist, Roles.Manager);
  }
}

internal sealed record IssueAccessCardRequest(
  ulong Uid,
  decimal Deposit,
  DateOnly ValidUntil,
  Guid? BillId,
  string? Note);

internal sealed record UpdateAccessCardRequest(
  DateOnly ValidUntil,
  string? Note);
