using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Operations.SpotGroupOOFItems;
using Domain.Operations.SpotOOFItems;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.ReservationStates;
using Domain.Reservations.SpotGroups;
using Domain.Reservations.Spots;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Spots;

public sealed record SpotResponse(Guid Id, Guid SpotGroupId, string Name, string? Description, bool IsActive);

public sealed record GetSpotsQuery : IQuery<List<SpotResponse>>;

internal sealed class GetSpotsQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetSpotsQuery, List<SpotResponse>>
{
  public async Task<Result<List<SpotResponse>>> Handle(
    GetSpotsQuery query,
    CancellationToken cancellationToken)
  {
    List<SpotResponse> spots = await context.Spots
      .Select(s => new SpotResponse(s.Id, s.SpotGroupId, s.Name, s.Description, s.IsActive))
      .ToListAsync(cancellationToken);

    return spots;
  }
}

public sealed record CreateSpotCommand(Guid SpotGroupId, string Name, string? Description, bool IsActive) : ICommand<Guid>;

internal sealed class CreateSpotCommandHandler(IApplicationDbContext context)
  : ICommandHandler<CreateSpotCommand, Guid>
{
  public async Task<Result<Guid>> Handle(
    CreateSpotCommand command,
    CancellationToken cancellationToken)
  {
    if (!await context.SpotGroups.AnyAsync(sg => sg.Id == command.SpotGroupId, cancellationToken))
    {
      return Result.Failure<Guid>(SpotGroupErrors.NotFound(command.SpotGroupId));
    }

    Spot spot = new()
    {
      Id = Guid.NewGuid(),
      SpotGroupId = command.SpotGroupId,
      Name = command.Name,
      Description = command.Description,
      IsActive = command.IsActive
    };

    context.Spots.Add(spot);
    await context.SaveChangesAsync(cancellationToken);

    return spot.Id;
  }
}

internal sealed class CreateSpotCommandValidator : AbstractValidator<CreateSpotCommand>
{
  public CreateSpotCommandValidator()
  {
    RuleFor(c => c.SpotGroupId)
      .NotEmpty();

    RuleFor(c => c.Name)
      .NotEmpty()
      .MaximumLength(255);

    RuleFor(c => c.Description)
      .MaximumLength(1000)
      .When(c => c.Description is not null);
  }
}

public sealed record UpdateSpotCommand(Guid Id, Guid SpotGroupId, string Name, string? Description, bool IsActive) : ICommand;

internal sealed class UpdateSpotCommandHandler(IApplicationDbContext context)
  : ICommandHandler<UpdateSpotCommand>
{
  public async Task<Result> Handle(
    UpdateSpotCommand command,
    CancellationToken cancellationToken)
  {
    Spot? spot = await context.Spots
      .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

    if (spot is null)
    {
      return Result.Failure(SpotErrors.NotFound(command.Id));
    }

    if (spot.SpotGroupId != command.SpotGroupId &&
        !await context.SpotGroups.AnyAsync(sg => sg.Id == command.SpotGroupId, cancellationToken))
    {
      return Result.Failure(SpotGroupErrors.NotFound(command.SpotGroupId));
    }

    spot.SpotGroupId = command.SpotGroupId;
    spot.Name = command.Name;
    spot.Description = command.Description;
    spot.IsActive = command.IsActive;

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class UpdateSpotCommandValidator : AbstractValidator<UpdateSpotCommand>
{
  public UpdateSpotCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();

    RuleFor(c => c.SpotGroupId)
      .NotEmpty();

    RuleFor(c => c.Name)
      .NotEmpty()
      .MaximumLength(255);

    RuleFor(c => c.Description)
      .MaximumLength(1000)
      .When(c => c.Description is not null);
  }
}

public sealed record DeleteSpotCommand(Guid Id) : ICommand;

internal sealed class DeleteSpotCommandHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteSpotCommand>
{
  public async Task<Result> Handle(
    DeleteSpotCommand command,
    CancellationToken cancellationToken)
  {
    Spot? spot = await context.Spots
      .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

    if (spot is null)
    {
      return Result.Failure(SpotErrors.NotFound(command.Id));
    }

    context.Spots.Remove(spot);
    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class DeleteSpotCommandValidator : AbstractValidator<DeleteSpotCommand>
{
  public DeleteSpotCommandValidator()
  {
    RuleFor(c => c.Id)
      .NotEmpty();
  }
}

public sealed record SpotStateResponse(
  Guid SpotId,
  SpotState State,
  DateOnly? DepartureDate,
  bool HasGivenKey,
  bool IsPaid);

public sealed record GetSpotStatesQuery : IQuery<List<SpotStateResponse>>;

internal sealed class GetSpotStatesQueryHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider)
  : IQueryHandler<GetSpotStatesQuery, List<SpotStateResponse>>
{
  public async Task<Result<List<SpotStateResponse>>> Handle(
    GetSpotStatesQuery query,
    CancellationToken cancellationToken)
  {
    DateTime nowUtc = dateTimeProvider.UtcNow;
    var today = DateOnly.FromDateTime(nowUtc);

    List<Guid> spotIds = await context.Spots
      .AsNoTracking()
      .Where(s => s.IsActive)
      .Select(s => s.Id)
      .ToListAsync(cancellationToken);

    HashSet<Guid> directOoo = await context.SpotOofItems
      .AsNoTracking()
      .Where(soi => context.OutOfOrders.Any(o => o.Id == soi.OutOfOrderId
                                              && o.Period.From <= today && o.Period.To >= today))
      .Select(soi => soi.SpotId)
      .ToHashSetAsync(cancellationToken);

    HashSet<Guid> groupOoo = await (
      from sgoi in context.SpotGroupOofItems.AsNoTracking()
      join s in context.Spots on sgoi.SpotGroupId equals s.SpotGroupId
      where context.OutOfOrders.Any(o => o.Id == sgoi.OutOfOrderId
                                      && o.Period.From <= today && o.Period.To >= today)
      select s.Id)
      .ToHashSetAsync(cancellationToken);

    HashSet<Guid> oooSpotIds = [.. directOoo, .. groupOoo];

    var bindings = await (
      from rsi in context.ReservationSpotItems.AsNoTracking()
      join r in context.Reservations on rsi.ReservationId equals r.Id
      where rsi.SpotId != null
        && (r.State == ReservationState.Confirmed || r.State == ReservationState.CheckedIn)
        && r.Period.From <= today && r.Period.To >= today
        && !rsi.HasReturnedKeys
      select new
      {
        SpotId = rsi.SpotId!.Value,
        ReservationState = r.State,
        r.Period.From,
        r.Period.To,
        rsi.HasGivenKey,
        IsPaid = rsi.BillId != null,
      })
      .ToListAsync(cancellationToken);

    Dictionary<Guid, (ReservationState State, DateOnly From, DateOnly To, bool HasGivenKey, bool IsPaid)> bindingBySpot =
      bindings
        .GroupBy(b => b.SpotId)
        .ToDictionary(
          g => g.Key,
          g =>
          {
            var pick = g
              .OrderByDescending(b => b.ReservationState == ReservationState.CheckedIn)
              .ThenByDescending(b => b.HasGivenKey)
              .First();
            return (pick.ReservationState, pick.From, pick.To, pick.HasGivenKey, pick.IsPaid);
          });

    List<SpotStateResponse> result = new(spotIds.Count);
    foreach (Guid spotId in spotIds)
    {
      if (oooSpotIds.Contains(spotId))
      {
        result.Add(new SpotStateResponse(spotId, SpotState.OutOfOrder, null, HasGivenKey: false, IsPaid: false));
        continue;
      }

      if (!bindingBySpot.TryGetValue(spotId, out (ReservationState State, DateOnly From, DateOnly To, bool HasGivenKey, bool IsPaid) b))
      {
        result.Add(new SpotStateResponse(spotId, SpotState.Unoccupied, null, HasGivenKey: false, IsPaid: false));
        continue;
      }

      // Holding the key proves arrival even before reservation-level CheckedIn
      // (which waits for all non-Czech guests to sign in).
      bool isPhysicallyPresent = b.State == ReservationState.CheckedIn || b.HasGivenKey;
      bool isLeavingToday = b.To == today;

      SpotState state = (isPhysicallyPresent, isLeavingToday) switch
      {
        (true, true) => SpotState.ExpectingDeparture,
        (true, false) => SpotState.Occupied,
        (false, _) => SpotState.ExpectingArrival,
      };

      DateOnly? departureDate =
        state is SpotState.Occupied or SpotState.ExpectingDeparture ? b.To : null;

      result.Add(new SpotStateResponse(spotId, state, departureDate, b.HasGivenKey, b.IsPaid));
    }

    return result;
  }
}
