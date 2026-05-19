using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Reservations;
using Domain.Reservations.Reservations;
using Domain.Reservations.ReservationStates;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Commands.CancelReservation;

internal sealed class CancelReservationCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider)
  : ICommandHandler<CancelReservationCommand>
{
  public async Task<Result> Handle(
    CancelReservationCommand command,
    CancellationToken cancellationToken)
  {
    Reservation? reservation = await context.Reservations
      .FirstOrDefaultAsync(r => r.Id == command.Id, cancellationToken);

    if (reservation is null)
    {
      return Result.Failure(ReservationErrors.NotFound(command.Id));
    }

    if (reservation.State == ReservationState.Cancelled)
    {
      return Result.Failure(ReservationErrors.AlreadyCancelled(command.Id));
    }

    reservation.State = ReservationState.Cancelled;
    reservation.UpdatedAtUtc = dateTimeProvider.UtcNow;

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}
