using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.Reservations;
using Domain.Reservations.GroupReservations;
using Domain.Reservations.GroupReservations.DomainEvents;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Commands.UpdateGroupReservation;

internal sealed class UpdateGroupReservationCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider)
  : ICommandHandler<UpdateGroupReservationCommand>
{
  public async Task<Result> Handle(
    UpdateGroupReservationCommand command,
    CancellationToken cancellationToken)
  {
    GroupReservation? group = await context.GroupReservations
      .Include(g => g.HeldSpots)
      .FirstOrDefaultAsync(g => g.Id == command.Id, cancellationToken);

    if (group is null)
    {
      return Result.Failure(GroupReservationErrors.NotFound(command.Id));
    }

    if (group.State == GroupReservationState.Canceled)
    {
      return Result.Failure(GroupReservationErrors.Canceled(command.Id));
    }

    var distinctSpotIds = command.SpotIds.Distinct().ToList();

    HashSet<Guid> existingSpotIds = await context.Spots
      .Where(s => distinctSpotIds.Contains(s.Id))
      .Select(s => s.Id)
      .ToHashSetAsync(cancellationToken);

    foreach (Guid spotId in distinctSpotIds)
    {
      if (!existingSpotIds.Contains(spotId))
      {
        return Result.Failure(ReservationErrors.SpotNotFound(spotId));
      }
    }

    group.Period = new DateRange(command.From, command.To);
    group.OrganizerName = command.OrganizerName;
    group.OrganizerEmail = command.OrganizerEmail;
    group.OrganizerPhone = command.OrganizerPhone;
    group.Note = command.Note;
    group.DisplayName = command.DisplayName;
    group.UpdatedAtUtc = dateTimeProvider.UtcNow;

    group.HeldSpots.Clear();
    foreach (Guid spotId in distinctSpotIds)
    {
      group.HeldSpots.Add(new GroupReservationSpot { SpotId = spotId });
    }

    group.Raise(new GroupReservationUpdatedDomainEvent(group.Id));

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}
