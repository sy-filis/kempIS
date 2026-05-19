using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Reservations;
using Application.Reservations.Commands.CreateReservation;
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

namespace Application.Reservations.Commands.UpdateReservation;

internal sealed class UpdateReservationCommandHandler(
  IApplicationDbContext context,
  ISpotAvailabilityChecker availabilityChecker,
  IDateTimeProvider dateTimeProvider)
  : ICommandHandler<UpdateReservationCommand>
{
  public async Task<Result> Handle(
    UpdateReservationCommand command,
    CancellationToken cancellationToken)
  {
    Reservation? reservation = await context.Reservations
      .FirstOrDefaultAsync(r => r.Id == command.Id, cancellationToken);

    if (reservation is null)
    {
      return Result.Failure(ReservationErrors.NotFound(command.Id));
    }

    if (reservation.State is ReservationState.Cancelled or ReservationState.Completed)
    {
      return Result.Failure(ReservationErrors.NotEditableInState(reservation.Id, reservation.State));
    }

    var distinctSpotIds = command.SpotIds.Distinct().ToList();

    Dictionary<Guid, Guid> spotLookup = await context.Spots
      .Where(s => distinctSpotIds.Contains(s.Id))
      .Select(s => new { s.Id, s.SpotGroupId })
      .ToDictionaryAsync(s => s.Id, s => s.SpotGroupId, cancellationToken);

    foreach (Guid spotId in distinctSpotIds)
    {
      if (!spotLookup.ContainsKey(spotId))
      {
        return Result.Failure(ReservationErrors.SpotNotFound(spotId));
      }
    }

    var requestedServiceIds = command.Services.Select(s => s.ServiceId).ToHashSet();
    HashSet<Guid> existingServiceIds = await context.Services
      .Where(s => requestedServiceIds.Contains(s.Id))
      .Select(s => s.Id)
      .ToHashSetAsync(cancellationToken);

    foreach (Guid serviceId in requestedServiceIds)
    {
      if (!existingServiceIds.Contains(serviceId))
      {
        return Result.Failure(ReservationErrors.ServiceNotFound(serviceId));
      }
    }

    List<ReservationSpotItem> existingSpotItems = await context.ReservationSpotItems
      .Where(s => s.ReservationId == command.Id)
      .ToListAsync(cancellationToken);

    List<ReservationServiceItem> existingServiceItems = await context.ReservationServiceItems
      .Where(s => s.ReservationId == command.Id)
      .ToListAsync(cancellationToken);

    List<Vehicle> existingVehicles = await context.Vehicles
      .Where(v => v.ReservationId == command.Id)
      .ToListAsync(cancellationToken);

    var existingVehicleIds = existingVehicles.Select(v => v.Id).ToHashSet();
    foreach (ReservationVehicleLine line in command.Vehicles)
    {
      if (line.Id is { } id && !existingVehicleIds.Contains(id))
      {
        return Result.Failure(ReservationErrors.VehicleNotOnReservation(id));
      }
    }

    var newPeriod = new DateRange(command.From, command.To);

    Result availability = await availabilityChecker.CheckAsync(
      distinctSpotIds,
      newPeriod,
      new SpotAvailabilityContext(
        ExcludeReservationId: command.Id,
        AllowGroupOverlap: command.GroupReservationId),
      cancellationToken);

    if (availability.IsFailure)
    {
      return Result.Failure(availability.Error);
    }

    reservation.ReservationMaker = new ReservationMaker(
      command.Name, command.Surname, command.Email, command.Phone);
    reservation.Period = newPeriod;
    reservation.Note = command.Note;
    reservation.DisplayName = command.DisplayName;
    reservation.GroupReservationId = command.GroupReservationId;
    reservation.Language = command.Language ?? ReservationLanguages.Czech;
    reservation.UpdatedAtUtc = dateTimeProvider.UtcNow;

    var desiredSpotIds = distinctSpotIds.ToHashSet();
    foreach (ReservationSpotItem existing in existingSpotItems)
    {
      Guid? key = existing.SpotId;
      if (key is null || !desiredSpotIds.Contains(key.Value))
      {
        if (existing.BillId is not null)
        {
          return Result.Failure(ReservationErrors.SpotItemPaidCannotBeRemoved(existing.Id));
        }
        context.ReservationSpotItems.Remove(existing);
      }
    }

    var keptSpotIds = existingSpotItems
      .Where(e => e.SpotId is { } id && desiredSpotIds.Contains(id))
      .Select(e => e.SpotId!.Value)
      .ToHashSet();

    foreach (Guid spotId in distinctSpotIds)
    {
      if (!keptSpotIds.Contains(spotId))
      {
        context.ReservationSpotItems.Add(new ReservationSpotItem
        {
          Id = Guid.NewGuid(),
          ReservationId = reservation.Id,
          SpotGroupId = spotLookup[spotId],
          SpotId = spotId,
        });
      }
    }

    var existingByServiceId = existingServiceItems.ToDictionary(s => s.ServiceId);
    foreach (ReservationServiceLine line in command.Services)
    {
      if (existingByServiceId.TryGetValue(line.ServiceId, out ReservationServiceItem? existing))
      {
        existing.Quantity = line.Quantity;
        existing.RecapSingleQuantity = line.RecapSingleQuantity;
        existing.RecapDayQuantity = line.RecapDayQuantity;
      }
      else
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
    }
    foreach (ReservationServiceItem existing in existingServiceItems)
    {
      if (!requestedServiceIds.Contains(existing.ServiceId))
      {
        context.ReservationServiceItems.Remove(existing);
      }
    }

    var existingByVehicleId = existingVehicles.ToDictionary(v => v.Id);
    HashSet<Guid> keptVehicleIds = [];
    foreach (ReservationVehicleLine line in command.Vehicles)
    {
      if (line.Id is { } id && existingByVehicleId.TryGetValue(id, out Vehicle? existing))
      {
        existing.RegistrationNumber = line.RegistrationNumber;
        keptVehicleIds.Add(id);
      }
      else
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
    }
    foreach (Vehicle existing in existingVehicles)
    {
      if (!keptVehicleIds.Contains(existing.Id))
      {
        if (existing.BillId is not null)
        {
          return Result.Failure(ReservationErrors.VehiclePaidCannotBeRemoved(existing.Id));
        }
        context.Vehicles.Remove(existing);
      }
    }

    reservation.ConfirmIfCreated();

    reservation.Raise(new ReservationUpdatedDomainEvent(reservation.Id));

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}
