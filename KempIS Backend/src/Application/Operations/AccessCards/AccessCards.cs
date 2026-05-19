using Application.Abstractions.Data;
using Application.Abstractions.Gate;
using Application.Abstractions.Messaging;
using Domain.Finance.Bills;
using Domain.Operations.AccessCards;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Operations.AccessCards;

public sealed record AccessCardBillSummary(
  Guid Id,
  string Number);

public sealed record AccessCardResponse(
  Guid Id,
  ulong Uid,
  decimal Deposit,
  DateOnly ValidUntil,
  DateTime IssuedAtUtc,
  string? Note,
  AccessCardBillSummary? Bill);

public sealed record IssueAccessCardCommand(
  Guid? BillId,
  ulong Uid,
  decimal Deposit,
  DateOnly ValidUntil,
  string? Note)
  : ICommand<AccessCardResponse>;

internal sealed class IssueAccessCardCommandValidator : AbstractValidator<IssueAccessCardCommand>
{
  public IssueAccessCardCommandValidator()
  {
    RuleFor(c => c.BillId)
      .NotEqual(Guid.Empty)
      .When(c => c.BillId is not null);
    RuleFor(c => c.Uid).GreaterThan(0UL);
    RuleFor(c => c.Deposit).GreaterThanOrEqualTo(0m);
    RuleFor(c => c.ValidUntil)
      .NotEqual(default(DateOnly));
  }
}

internal sealed class IssueAccessCardCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider,
  IGateClient gateClient,
  ILogger<IssueAccessCardCommandHandler> logger)
  : ICommandHandler<IssueAccessCardCommand, AccessCardResponse>
{
  private static readonly TimeZoneInfo CampTimeZone =
    TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");

  public async Task<Result<AccessCardResponse>> Handle(
    IssueAccessCardCommand command,
    CancellationToken cancellationToken)
  {
    AccessCardBillSummary? billSummary = null;
    string realName = string.Empty;
    if (command.BillId is { } billId)
    {
      var billRow = await context.Bills
        .AsNoTracking()
        .Where(b => b.Id == billId)
        .Select(b => new
        {
          b.Id,
          b.Number,
          PayerName = b.Payer.Name,
          PayerSurname = b.Payer.Surname,
        })
        .FirstOrDefaultAsync(cancellationToken);
      if (billRow is null)
      {
        return Result.Failure<AccessCardResponse>(BillErrors.NotFound(billId));
      }
      billSummary = new AccessCardBillSummary(billRow.Id, billRow.Number);
      realName = $"{billRow.PayerName} {billRow.PayerSurname}".Trim();
    }

    bool uidInUse = await context.AccessCards
      .AnyAsync(c => c.Uid == command.Uid, cancellationToken);
    if (uidInUse)
    {
      return Result.Failure<AccessCardResponse>(AccessCardErrors.UidAlreadyInUse(command.Uid));
    }

    AccessCard card = new()
    {
      Id = Guid.NewGuid(),
      Uid = command.Uid,
      BillId = command.BillId,
      Deposit = command.Deposit,
      ValidUntil = command.ValidUntil,
      IssuedAtUtc = dateTimeProvider.UtcNow,
      Note = command.Note,
    };

    context.AccessCards.Add(card);
    await context.SaveChangesAsync(cancellationToken);

    await TryPushToGateAsync(card, realName, cancellationToken);

    return new AccessCardResponse(
      card.Id, card.Uid, card.Deposit, card.ValidUntil, card.IssuedAtUtc, card.Note, billSummary);
  }

  private async Task TryPushToGateAsync(
    AccessCard card, string realName, CancellationToken cancellationToken)
  {
    // Unspecified Kind makes GetUtcOffset treat the value as local wall-clock for DST.
    var localEndOfDay = card.ValidUntil.ToDateTime(new TimeOnly(23, 59, 59));
    var validTo = new DateTimeOffset(localEndOfDay, CampTimeZone.GetUtcOffset(localEndOfDay));

    var payload = new GateCardPayload(validTo, realName, card.Note ?? string.Empty);

    try
    {
      await gateClient.PutCardAsync(card.Uid, payload, cancellationToken);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogWarning(ex,
        "Gate webhook PUT failed for card uid {Uid} (bill {BillId}); DB write kept.",
        card.Uid, card.BillId);
    }
  }
}

public sealed record ListAccessCardsQuery : IQuery<IReadOnlyList<AccessCardResponse>>;

internal sealed class ListAccessCardsQueryHandler(IApplicationDbContext context)
  : IQueryHandler<ListAccessCardsQuery, IReadOnlyList<AccessCardResponse>>
{
  public async Task<Result<IReadOnlyList<AccessCardResponse>>> Handle(
    ListAccessCardsQuery query,
    CancellationToken cancellationToken)
  {
    List<AccessCardResponse> cards = await context.AccessCards
      .AsNoTracking()
      .OrderByDescending(c => c.IssuedAtUtc)
      .Select(c => new AccessCardResponse(
        c.Id,
        c.Uid,
        c.Deposit,
        c.ValidUntil,
        c.IssuedAtUtc,
        c.Note,
        c.BillId == null
          ? null
          : context.Bills
              .Where(b => b.Id == c.BillId)
              .Select(b => new AccessCardBillSummary(b.Id, b.Number))
              .FirstOrDefault()))
      .ToListAsync(cancellationToken);

    return cards;
  }
}

public sealed record UpdateAccessCardCommand(
  Guid Id,
  DateOnly ValidUntil,
  string? Note)
  : ICommand<AccessCardResponse>;

internal sealed class UpdateAccessCardCommandValidator : AbstractValidator<UpdateAccessCardCommand>
{
  public UpdateAccessCardCommandValidator()
  {
    RuleFor(c => c.Id).NotEqual(Guid.Empty);
    RuleFor(c => c.ValidUntil)
      .NotEqual(default(DateOnly));
  }
}

internal sealed class UpdateAccessCardCommandHandler(
  IApplicationDbContext context,
  IGateClient gateClient,
  ILogger<UpdateAccessCardCommandHandler> logger)
  : ICommandHandler<UpdateAccessCardCommand, AccessCardResponse>
{
  private static readonly TimeZoneInfo CampTimeZone =
    TimeZoneInfo.FindSystemTimeZoneById("Europe/Prague");

  public async Task<Result<AccessCardResponse>> Handle(
    UpdateAccessCardCommand command,
    CancellationToken cancellationToken)
  {
    AccessCard? card = await context.AccessCards
      .FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
    if (card is null)
    {
      return Result.Failure<AccessCardResponse>(AccessCardErrors.NotFound(command.Id));
    }

    AccessCardBillSummary? billSummary = null;
    string realName = string.Empty;
    if (card.BillId is { } billId)
    {
      var billRow = await context.Bills
        .AsNoTracking()
        .Where(b => b.Id == billId)
        .Select(b => new
        {
          b.Id,
          b.Number,
          PayerName = b.Payer.Name,
          PayerSurname = b.Payer.Surname,
        })
        .FirstOrDefaultAsync(cancellationToken);
      if (billRow is not null)
      {
        billSummary = new AccessCardBillSummary(billRow.Id, billRow.Number);
        realName = $"{billRow.PayerName} {billRow.PayerSurname}".Trim();
      }
    }

    card.ValidUntil = command.ValidUntil;
    card.Note = command.Note;

    await context.SaveChangesAsync(cancellationToken);

    await TryPushToGateAsync(card, realName, cancellationToken);

    return new AccessCardResponse(
      card.Id, card.Uid, card.Deposit, card.ValidUntil, card.IssuedAtUtc, card.Note, billSummary);
  }

  private async Task TryPushToGateAsync(
    AccessCard card, string realName, CancellationToken cancellationToken)
  {
    // Unspecified Kind makes GetUtcOffset treat the value as local wall-clock for DST.
    var localEndOfDay = card.ValidUntil.ToDateTime(new TimeOnly(23, 59, 59));
    var validTo = new DateTimeOffset(localEndOfDay, CampTimeZone.GetUtcOffset(localEndOfDay));

    var payload = new GateCardPayload(validTo, realName, card.Note ?? string.Empty);

    try
    {
      await gateClient.PutCardAsync(card.Uid, payload, cancellationToken);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogWarning(ex,
        "Gate webhook PUT failed for card uid {Uid} (bill {BillId}); DB update kept.",
        card.Uid, card.BillId);
    }
  }
}

public sealed record ReturnAccessCardCommand(Guid Id) : ICommand;

internal sealed class ReturnAccessCardCommandHandler(
  IApplicationDbContext context,
  IGateClient gateClient,
  ILogger<ReturnAccessCardCommandHandler> logger)
  : ICommandHandler<ReturnAccessCardCommand>
{
  public async Task<Result> Handle(
    ReturnAccessCardCommand command,
    CancellationToken cancellationToken)
  {
    AccessCard? card = await context.AccessCards
      .FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);
    if (card is null)
    {
      return Result.Failure(AccessCardErrors.NotFound(command.Id));
    }

    ulong uid = card.Uid;

    context.AccessCards.Remove(card);
    await context.SaveChangesAsync(cancellationToken);

    try
    {
      await gateClient.DeleteCardAsync(uid, cancellationToken);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogWarning(ex,
        "Gate webhook DELETE failed for card uid {Uid}; DB row already removed.", uid);
    }

    return Result.Success();
  }
}
