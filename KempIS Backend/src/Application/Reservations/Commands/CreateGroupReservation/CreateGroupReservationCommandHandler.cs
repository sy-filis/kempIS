using System.Security.Cryptography;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Reservations;
using Domain.Common;
using Domain.Reservations;
using Domain.Reservations.GroupReservations;
using Domain.Reservations.GroupReservations.DomainEvents;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Commands.CreateGroupReservation;

internal sealed class CreateGroupReservationCommandHandler(
  IApplicationDbContext context,
  ISpotAvailabilityChecker availabilityChecker,
  IDateTimeProvider dateTimeProvider,
  IGroupReservationNumberGenerator numberGenerator)
  : ICommandHandler<CreateGroupReservationCommand, CreateGroupReservationResponse>
{
  public async Task<Result<CreateGroupReservationResponse>> Handle(
    CreateGroupReservationCommand command,
    CancellationToken cancellationToken)
  {
    var distinctSpotIds = command.SpotIds.Distinct().ToList();

    HashSet<Guid> existingSpotIds = await context.Spots
      .Where(s => distinctSpotIds.Contains(s.Id))
      .Select(s => s.Id)
      .ToHashSetAsync(cancellationToken);

    foreach (Guid spotId in distinctSpotIds)
    {
      if (!existingSpotIds.Contains(spotId))
      {
        return Result.Failure<CreateGroupReservationResponse>(ReservationErrors.SpotNotFound(spotId));
      }
    }

    var period = new DateRange(command.From, command.To);

    Result availability = await availabilityChecker.CheckAsync(
      distinctSpotIds,
      period,
      new SpotAvailabilityContext(),
      cancellationToken);

    if (availability.IsFailure)
    {
      return Result.Failure<CreateGroupReservationResponse>(availability.Error);
    }

    string secret = GenerateSecret();
    DateTime now = dateTimeProvider.UtcNow;
    string number = await numberGenerator.NextAsync(now.Year, cancellationToken);

    GroupReservation group = new()
    {
      Id = Guid.NewGuid(),
      Number = number,
      State = GroupReservationState.Confirmed,
      Period = period,
      Secret = secret,
      OrganizerName = command.OrganizerName,
      OrganizerEmail = command.OrganizerEmail,
      OrganizerPhone = command.OrganizerPhone,
      Note = command.Note,
      DisplayName = command.DisplayName,
      Language = command.Language,
      CreatedAtUtc = now,
      HeldSpots = distinctSpotIds
        .Select(spotId => new GroupReservationSpot { SpotId = spotId })
        .ToList()
    };

    group.Raise(new GroupReservationCreatedDomainEvent(group.Id));

    context.GroupReservations.Add(group);

    await context.SaveChangesAsync(cancellationToken);

    return new CreateGroupReservationResponse(group.Id, group.Number, secret);
  }

  private static string GenerateSecret()
  {
    Span<byte> buffer = stackalloc byte[32];
    RandomNumberGenerator.Fill(buffer);
    return Convert.ToHexStringLower(buffer);
  }
}
