using System.Security.Cryptography;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Reservations;
using Domain.Common;
using Domain.Reservations;
using Domain.Reservations.GroupReservations;
using Domain.Reservations.ReservationMakers;
using Domain.Reservations.Reservations;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.ReservationStates;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Commands.CreateWebReservation;

internal sealed class CreateWebReservationCommandHandler(
  IApplicationDbContext context,
  IDateTimeProvider dateTimeProvider,
  IReservationNumberGenerator numberGenerator)
  : ICommandHandler<CreateWebReservationCommand, CreateWebReservationResponse>
{
  public async Task<Result<CreateWebReservationResponse>> Handle(
    CreateWebReservationCommand command,
    CancellationToken cancellationToken)
  {
    Guid? allowedGroupId = null;
    if (command.GroupReservationId is not null)
    {
      GroupReservation? group = await context.GroupReservations
        .FirstOrDefaultAsync(g => g.Id == command.GroupReservationId, cancellationToken);

      if (group is null)
      {
        return Result.Failure<CreateWebReservationResponse>(GroupReservationErrors.NotFound(command.GroupReservationId.Value));
      }

      if (group.State != GroupReservationState.Confirmed)
      {
        return Result.Failure<CreateWebReservationResponse>(GroupReservationErrors.Canceled(group.Id));
      }

      if (!string.Equals(group.Secret, command.GroupReservationSecret, StringComparison.Ordinal))
      {
        return Result.Failure<CreateWebReservationResponse>(GroupReservationErrors.SecretInvalid);
      }

      if (group.Period.From > command.To || group.Period.To < command.From)
      {
        return Result.Failure<CreateWebReservationResponse>(GroupReservationErrors.PeriodOutsideGroup(group.Id));
      }

      allowedGroupId = group.Id;
    }

    var requestedGroupIds = command.RequestedSpots.Select(r => r.SpotGroupId).Distinct().ToList();

    Dictionary<Guid, (string Name, bool IsActive)> groupLookup = await context.SpotGroups
      .Where(sg => requestedGroupIds.Contains(sg.Id))
      .Select(sg => new { sg.Id, sg.Name, sg.IsActive })
      .ToDictionaryAsync(sg => sg.Id, sg => (sg.Name, sg.IsActive), cancellationToken);

    foreach (RequestedSpotGroup req in command.RequestedSpots)
    {
      if (!groupLookup.TryGetValue(req.SpotGroupId, out (string Name, bool IsActive) g))
      {
        return Result.Failure<CreateWebReservationResponse>(ReservationErrors.SpotGroupNotFound(req.SpotGroupId));
      }

      if (!g.IsActive)
      {
        return Result.Failure<CreateWebReservationResponse>(ReservationErrors.SpotGroupInactive(req.SpotGroupId));
      }
    }

    Dictionary<Guid, int> totalSpotsByGroup = await context.Spots
      .Where(s => s.IsActive && requestedGroupIds.Contains(s.SpotGroupId))
      .GroupBy(s => s.SpotGroupId)
      .Select(g => new { SpotGroupId = g.Key, Count = g.Count() })
      .ToDictionaryAsync(x => x.SpotGroupId, x => x.Count, cancellationToken);

    Dictionary<Guid, int> reservedByGroup = await (
      from rsi in context.ReservationSpotItems
      join r in context.Reservations on rsi.ReservationId equals r.Id
      where rsi.SpotId != null
        && requestedGroupIds.Contains(rsi.SpotGroupId)
        && (r.State == ReservationState.Confirmed || r.State == ReservationState.CheckedIn)
        && r.Period.From <= command.To
        && r.Period.To >= command.From
      group rsi by rsi.SpotGroupId into g
      select new { SpotGroupId = g.Key, Count = g.Count() }
    ).ToDictionaryAsync(x => x.SpotGroupId, x => x.Count, cancellationToken);

    List<Guid> fullyOooList = await (
      from item in context.SpotGroupOofItems
      join oof in context.OutOfOrders on item.OutOfOrderId equals oof.Id
      where requestedGroupIds.Contains(item.SpotGroupId)
        && oof.Period.From <= command.To
        && oof.Period.To >= command.From
      select item.SpotGroupId
    ).Distinct().ToListAsync(cancellationToken);

    var groupsFullyOoo = fullyOooList.ToHashSet();

    Dictionary<Guid, int> spotOooByGroup = await (
      from item in context.SpotOofItems
      join oof in context.OutOfOrders on item.OutOfOrderId equals oof.Id
      join spot in context.Spots on item.SpotId equals spot.Id
      where requestedGroupIds.Contains(spot.SpotGroupId)
        && oof.Period.From <= command.To
        && oof.Period.To >= command.From
      group spot by spot.SpotGroupId into g
      select new { SpotGroupId = g.Key, Count = g.Select(s => s.Id).Distinct().Count() }
    ).ToDictionaryAsync(x => x.SpotGroupId, x => x.Count, cancellationToken);

    Dictionary<Guid, int> groupHeldByGroup = await (
      from grs in context.GroupReservationSpots
      join gr in context.GroupReservations on grs.GroupReservationId equals gr.Id
      join spot in context.Spots on grs.SpotId equals spot.Id
      where requestedGroupIds.Contains(spot.SpotGroupId)
        && gr.State == GroupReservationState.Confirmed
        && (allowedGroupId == null || gr.Id != allowedGroupId)
        && gr.Period.From <= command.To
        && gr.Period.To >= command.From
      group spot by spot.SpotGroupId into g
      select new { SpotGroupId = g.Key, Count = g.Select(s => s.Id).Distinct().Count() }
    ).ToDictionaryAsync(x => x.SpotGroupId, x => x.Count, cancellationToken);

    foreach (RequestedSpotGroup req in command.RequestedSpots)
    {
      int totalSpots = totalSpotsByGroup.TryGetValue(req.SpotGroupId, out int ts) ? ts : 0;
      int reservedCount = reservedByGroup.TryGetValue(req.SpotGroupId, out int rc) ? rc : 0;
      int spotOooCount = spotOooByGroup.TryGetValue(req.SpotGroupId, out int sc) ? sc : 0;
      int oooCount = groupsFullyOoo.Contains(req.SpotGroupId) ? totalSpots : spotOooCount;
      int groupHeldCount = groupHeldByGroup.TryGetValue(req.SpotGroupId, out int gc) ? gc : 0;
      int available = Math.Max(0, totalSpots - reservedCount - oooCount - groupHeldCount);

      if (req.Quantity > available)
      {
        return Result.Failure<CreateWebReservationResponse>(ReservationErrors.RequestedQuantityExceedsCapacity(req.SpotGroupId));
      }
    }

    string secret = GenerateSecret();
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
      GroupReservationId = allowedGroupId,
      Period = new DateRange(command.From, command.To),
      State = ReservationState.Created,
      CreatedAtUtc = now,
      Note = command.Note,
      Secret = secret,
      Language = command.Language ?? ReservationLanguages.Czech,
    };

    reservation.Raise(new ReservationCreatedDomainEvent(reservation.Id));

    context.Reservations.Add(reservation);

    foreach (RequestedSpotGroup req in command.RequestedSpots)
    {
      for (uint i = 0; i < req.Quantity; i++)
      {
        ReservationSpotItem spotItem = new()
        {
          Id = Guid.NewGuid(),
          ReservationId = reservation.Id,
          SpotGroupId = req.SpotGroupId,
          SpotId = null
        };

        context.ReservationSpotItems.Add(spotItem);
      }
    }

    await context.SaveChangesAsync(cancellationToken);

    return new CreateWebReservationResponse(reservation.Id, reservation.Number);
  }

  private static string GenerateSecret()
  {
    Span<byte> buffer = stackalloc byte[32];
    RandomNumberGenerator.Fill(buffer);
    return Convert.ToHexStringLower(buffer);
  }
}
