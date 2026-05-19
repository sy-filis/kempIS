using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Reservations;
using Domain.Reservations.Reservations;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.ReservationStates;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.ReservationSpotItems.Commands.ReturnKeys;

internal sealed class ReturnKeysCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider)
  : ICommandHandler<ReturnKeysCommand>
{
  public async Task<Result> Handle(
    ReturnKeysCommand command,
    CancellationToken cancellationToken)
  {
    ReservationSpotItem? item = await context.ReservationSpotItems
      .FirstOrDefaultAsync(rsi => rsi.Id == command.Id, cancellationToken);
    if (item is null)
    {
      return Result.Failure(ReservationSpotItemErrors.NotFound(command.Id));
    }

    Reservation? reservation = await context.Reservations
      .FirstOrDefaultAsync(r => r.Id == item.ReservationId, cancellationToken);
    if (reservation is null)
    {
      return Result.Failure(ReservationErrors.NotFound(item.ReservationId));
    }

    // Idempotent: state gate fires only when actually flipping the flag.
    if (!item.HasReturnedKeys && reservation.State != ReservationState.CheckedIn)
    {
      return Result.Failure(ReservationErrors.CannotReturnKeysReservationNotCheckedIn(reservation.Id));
    }

    item.HasReturnedKeys = true;

    List<ReservationSpotItem> siblings = await context.ReservationSpotItems
      .Where(rsi => rsi.ReservationId == item.ReservationId)
      .ToListAsync(cancellationToken);

    Result transition = reservation.MarkCompletedIfAllKeysReturned(siblings);
    if (transition.IsFailure)
    {
      return transition;
    }

    reservation.UpdatedAtUtc = dateTimeProvider.UtcNow;
    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}
