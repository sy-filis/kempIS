using Application.Abstractions.Data;
using Application.Abstractions.Reservations;
using Domain.Common;
using Domain.Reservations;
using Domain.Reservations.GroupReservations;
using Domain.Reservations.ReservationStates;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Services;

internal sealed class SpotAvailabilityChecker(IApplicationDbContext dbContext)
  : ISpotAvailabilityChecker
{
  public async Task<Result> CheckAsync(
    IReadOnlyCollection<Guid> spotIds,
    DateRange period,
    SpotAvailabilityContext context,
    CancellationToken cancellationToken)
  {
    if (spotIds.Count == 0)
    {
      return Result.Success();
    }

    Guid? occupiedByReservation = await (
      from rsi in dbContext.ReservationSpotItems
      join r in dbContext.Reservations on rsi.ReservationId equals r.Id
      where rsi.SpotId != null
        && spotIds.Contains(rsi.SpotId!.Value)
        && (context.ExcludeReservationId == null || rsi.ReservationId != context.ExcludeReservationId)
        && (r.State == ReservationState.Confirmed || r.State == ReservationState.CheckedIn)
        && r.Period.From <= period.To
        && r.Period.To >= period.From
      select rsi.SpotId
    ).FirstOrDefaultAsync(cancellationToken);

    if (occupiedByReservation is not null)
    {
      return Result.Failure(ReservationErrors.SpotOccupiedByReservation(occupiedByReservation.Value));
    }

    Guid? spotLevelOoo = await (
      from item in dbContext.SpotOofItems
      join oof in dbContext.OutOfOrders on item.OutOfOrderId equals oof.Id
      where spotIds.Contains(item.SpotId)
        && oof.Period.From <= period.To
        && oof.Period.To >= period.From
      select (Guid?)item.SpotId
    ).FirstOrDefaultAsync(cancellationToken);

    if (spotLevelOoo is not null)
    {
      return Result.Failure(ReservationErrors.SpotOccupiedByOutOfOrder(spotLevelOoo.Value));
    }

    Guid? groupLevelOoo = await (
      from spot in dbContext.Spots
      join item in dbContext.SpotGroupOofItems on spot.SpotGroupId equals item.SpotGroupId
      join oof in dbContext.OutOfOrders on item.OutOfOrderId equals oof.Id
      where spotIds.Contains(spot.Id)
        && oof.Period.From <= period.To
        && oof.Period.To >= period.From
      select (Guid?)spot.Id
    ).FirstOrDefaultAsync(cancellationToken);

    if (groupLevelOoo is not null)
    {
      return Result.Failure(ReservationErrors.SpotOccupiedByOutOfOrder(groupLevelOoo.Value));
    }

    Guid? heldByGroup = await (
      from grs in dbContext.GroupReservationSpots
      join gr in dbContext.GroupReservations on grs.GroupReservationId equals gr.Id
      where spotIds.Contains(grs.SpotId)
        && gr.State == GroupReservationState.Confirmed
        && (context.ExcludeGroupReservationId == null || gr.Id != context.ExcludeGroupReservationId)
        && (context.AllowGroupOverlap == null || gr.Id != context.AllowGroupOverlap)
        && gr.Period.From <= period.To
        && gr.Period.To >= period.From
      select (Guid?)grs.SpotId
    ).FirstOrDefaultAsync(cancellationToken);

    if (heldByGroup is not null)
    {
      return Result.Failure(ReservationErrors.SpotOccupiedByGroupReservation(heldByGroup.Value));
    }

    return Result.Success();
  }
}
