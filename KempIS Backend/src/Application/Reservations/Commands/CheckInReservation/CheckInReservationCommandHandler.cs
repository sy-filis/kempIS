using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Reservations;
using Domain.Reservations.Guests;
using Domain.Reservations.Reservations;
using Domain.Reservations.ReservationStates;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Commands.CheckInReservation;

internal sealed class CheckInReservationCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider)
  : ICommandHandler<CheckInReservationCommand>
{
  public async Task<Result> Handle(
    CheckInReservationCommand command,
    CancellationToken cancellationToken)
  {
    Reservation? reservation = await context.Reservations
      .FirstOrDefaultAsync(r => r.Id == command.Id, cancellationToken);

    if (reservation is null)
    {
      return Result.Failure(ReservationErrors.NotFound(command.Id));
    }

    if (reservation.State is not ReservationState.Created and not ReservationState.Confirmed)
    {
      return Result.Failure(ReservationErrors.InvalidStateForCheckIn(command.Id));
    }

    List<Guid> missingSignatures = await context.Guests
      .AsNoTracking()
      .Where(g => g.ReservationId == command.Id)
      .Where(g => g.Nationality!.Alpha2 != "CZ")
      .Where(g => g.SignaturePng == null)
      .Select(g => g.Id)
      .ToListAsync(cancellationToken);

    if (missingSignatures.Count > 0)
    {
      return Result.Failure(ReservationErrors.MissingGuestSignatures(missingSignatures));
    }

    reservation.State = ReservationState.CheckedIn;
    reservation.UpdatedAtUtc = dateTimeProvider.UtcNow;

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}
