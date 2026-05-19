using System.Security.Cryptography;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Reservations;
using Domain.Common;
using Domain.Reservations;
using Domain.Reservations.ReservationMakers;
using Domain.Reservations.Reservations;
using Domain.Reservations.ReservationServiceItems;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.ReservationStates;
using Domain.Reservations.Vehicles;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Commands.CreateReservation;

internal sealed class CreateReservationCommandHandler(
  IApplicationDbContext context,
  ISpotAvailabilityChecker availabilityChecker,
  IDateTimeProvider dateTimeProvider,
  IReservationNumberGenerator numberGenerator)
  : ICommandHandler<CreateReservationCommand, Guid>
{
  public async Task<Result<Guid>> Handle(
    CreateReservationCommand command,
    CancellationToken cancellationToken)
  {
    Dictionary<Guid, Guid> spotLookup = await context.Spots
      .Where(s => command.SpotIds.Contains(s.Id))
      .Select(s => new { s.Id, s.SpotGroupId })
      .ToDictionaryAsync(s => s.Id, s => s.SpotGroupId, cancellationToken);

    foreach (Guid spotId in command.SpotIds)
    {
      if (!spotLookup.ContainsKey(spotId))
      {
        return Result.Failure<Guid>(ReservationErrors.SpotNotFound(spotId));
      }
    }

    var serviceIds = command.Services.Select(s => s.ServiceId).ToHashSet();
    HashSet<Guid> existingServiceIds = await context.Services
      .Where(s => serviceIds.Contains(s.Id))
      .Select(s => s.Id)
      .ToHashSetAsync(cancellationToken);

    foreach (Guid serviceId in serviceIds)
    {
      if (!existingServiceIds.Contains(serviceId))
      {
        return Result.Failure<Guid>(ReservationErrors.ServiceNotFound(serviceId));
      }
    }

    var period = new DateRange(command.From, command.To);

    Result availability = await availabilityChecker.CheckAsync(
      command.SpotIds,
      period,
      new SpotAvailabilityContext(AllowGroupOverlap: command.GroupReservationId),
      cancellationToken);

    if (availability.IsFailure)
    {
      return Result.Failure<Guid>(availability.Error);
    }

    DateTime now = dateTimeProvider.UtcNow;
    string number = await numberGenerator.NextAsync(now.Year, cancellationToken);

    Reservation reservation = new()
    {
      Id = Guid.NewGuid(),
      Number = number,
      ReservationMaker = new ReservationMaker(
        command.Name,
        command.Surname,
        command.Email,
        command.Phone),
      GroupReservationId = command.GroupReservationId,
      Period = period,
      State = ReservationState.Confirmed,
      CreatedAtUtc = now,
      Note = command.Note,
      DisplayName = command.DisplayName,
      Secret = GenerateSecret(),
      Language = command.Language ?? ReservationLanguages.Czech,
    };

    reservation.Raise(new ReservationCreatedDomainEvent(reservation.Id));
    reservation.Raise(new ReservationConfirmedDomainEvent(reservation.Id));

    context.Reservations.Add(reservation);

    foreach (Guid spotId in command.SpotIds)
    {
      ReservationSpotItem spotItem = new()
      {
        Id = Guid.NewGuid(),
        ReservationId = reservation.Id,
        SpotGroupId = spotLookup[spotId],
        SpotId = spotId
      };

      context.ReservationSpotItems.Add(spotItem);
    }

    foreach (ReservationServiceLine line in command.Services)
    {
      context.ReservationServiceItems.Add(new ReservationServiceItem
      {
        Id = Guid.NewGuid(),
        ReservationId = reservation.Id,
        ServiceId = line.ServiceId,
        Quantity = line.Quantity,
        RecapSingleQuantity = line.RecapSingleQuantity,
        RecapDayQuantity = line.RecapDayQuantity,
      });
    }

    foreach (ReservationVehicleLine line in command.Vehicles)
    {
      context.Vehicles.Add(new Vehicle
      {
        Id = Guid.NewGuid(),
        ReservationId = reservation.Id,
        BillId = null,
        ServiceId = null,
        RegistrationNumber = line.RegistrationNumber,
      });
    }

    await context.SaveChangesAsync(cancellationToken);

    return reservation.Id;
  }

  private static string GenerateSecret()
  {
    Span<byte> buffer = stackalloc byte[32];
    RandomNumberGenerator.Fill(buffer);
    return Convert.ToHexStringLower(buffer);
  }
}
