using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Reservations;
using Domain.Reservations.Reservations;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.ReservationStates;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.ReservationSpotItems.Commands.GiveKey;

internal sealed class GiveKeyCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider)
  : ICommandHandler<GiveKeyCommand>
{
  public async Task<Result> Handle(
    GiveKeyCommand command,
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
    if (!item.HasGivenKey
        && reservation.State is not ReservationState.Confirmed
        and not ReservationState.CheckedIn)
    {
      return Result.Failure(
        ReservationSpotItemErrors.CannotGiveKeyReservationNotConfirmedOrCheckedIn);
    }

    item.HasGivenKey = true;
    reservation.UpdatedAtUtc = dateTimeProvider.UtcNow;

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}
