using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Reservations;
using Domain.Reservations.GroupReservations;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Commands.CancelGroupReservation;

internal sealed class CancelGroupReservationCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider)
  : ICommandHandler<CancelGroupReservationCommand>
{
  public async Task<Result> Handle(
    CancelGroupReservationCommand command,
    CancellationToken cancellationToken)
  {
    GroupReservation? group = await context.GroupReservations
      .FirstOrDefaultAsync(g => g.Id == command.Id, cancellationToken);

    if (group is null)
    {
      return Result.Failure(GroupReservationErrors.NotFound(command.Id));
    }

    if (group.State == GroupReservationState.Canceled)
    {
      return Result.Failure(GroupReservationErrors.AlreadyCanceled(command.Id));
    }

    group.State = GroupReservationState.Canceled;
    group.UpdatedAtUtc = dateTimeProvider.UtcNow;

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}
