using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Reservations.GroupReservations;
using Domain.Reservations.ReservationStates;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Queries.GetAvailability;

internal sealed class GetAvailabilityQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetAvailabilityQuery, AvailabilityResponse>
{
  public async Task<Result<AvailabilityResponse>> Handle(
    GetAvailabilityQuery query,
    CancellationToken cancellationToken)
  {
    Guid? allowedGroupId = null;
    if (query.GroupReservationId is not null)
    {
      GroupReservation? group = await context.GroupReservations
        .FirstOrDefaultAsync(g => g.Id == query.GroupReservationId, cancellationToken);

      if (group is not null
          && group.State == GroupReservationState.Confirmed
          && string.Equals(group.Secret, query.GroupReservationSecret, StringComparison.Ordinal))
      {
        allowedGroupId = group.Id;
      }
    }

    List<SpotGroupRow> groups = await context.SpotGroups
      .Where(sg => sg.IsActive)
      .Select(sg => new SpotGroupRow(sg.Id, sg.Name, sg.Capacity, sg.ImageUrl, sg.DetailsUrl))
      .ToListAsync(cancellationToken);

    Dictionary<Guid, int> totalSpotsByGroup = await context.Spots
      .Where(s => s.IsActive)
      .GroupBy(s => s.SpotGroupId)
      .Select(g => new { SpotGroupId = g.Key, Count = g.Count() })
      .ToDictionaryAsync(x => x.SpotGroupId, x => x.Count, cancellationToken);

    List<ReservedRow> reserved = await (
      from rsi in context.ReservationSpotItems
      join r in context.Reservations on rsi.ReservationId equals r.Id
      where rsi.SpotId != null
        && (r.State == ReservationState.Confirmed || r.State == ReservationState.CheckedIn)
        && r.Period.From <= query.To
        && r.Period.To >= query.From
      group rsi by rsi.SpotGroupId into g
      select new ReservedRow(g.Key, g.Count())
    ).ToListAsync(cancellationToken);

    var reservedByGroup = reserved.ToDictionary(r => r.SpotGroupId, r => r.Count);

    List<Guid> fullyOooList = await (
      from item in context.SpotGroupOofItems
      join oof in context.OutOfOrders on item.OutOfOrderId equals oof.Id
      where oof.Period.From <= query.To && oof.Period.To >= query.From
      select item.SpotGroupId
    ).Distinct().ToListAsync(cancellationToken);

    var groupsFullyOoo = fullyOooList.ToHashSet();

    List<SpotOooRow> spotOoo = await (
      from item in context.SpotOofItems
      join oof in context.OutOfOrders on item.OutOfOrderId equals oof.Id
      join spot in context.Spots on item.SpotId equals spot.Id
      where oof.Period.From <= query.To && oof.Period.To >= query.From
      group spot by spot.SpotGroupId into g
      select new SpotOooRow(g.Key, g.Select(s => s.Id).Distinct().Count())
    ).ToListAsync(cancellationToken);

    var spotOooByGroup = spotOoo.ToDictionary(s => s.SpotGroupId, s => s.Count);

    List<GroupHeldRow> groupHeld = await (
      from grs in context.GroupReservationSpots
      join gr in context.GroupReservations on grs.GroupReservationId equals gr.Id
      join spot in context.Spots on grs.SpotId equals spot.Id
      where gr.State == GroupReservationState.Confirmed
        && (allowedGroupId == null || gr.Id != allowedGroupId)
        && gr.Period.From <= query.To
        && gr.Period.To >= query.From
      group spot by spot.SpotGroupId into g
      select new GroupHeldRow(g.Key, g.Select(s => s.Id).Distinct().Count())
    ).ToListAsync(cancellationToken);

    var groupHeldByGroup = groupHeld.ToDictionary(g => g.SpotGroupId, g => g.Count);

    var activeGroupIds = groups.Select(g => g.Id).ToHashSet();

    List<EventRow> eventRows = await (
      from ev in context.Events
      where ev.StartsAt <= query.To
        && (ev.EndsAt == null || ev.EndsAt >= query.From)
      select new EventRow(
        ev.Id,
        ev.Name,
        ev.Description,
        ev.StartsAt,
        ev.EndsAt,
        ev.SpotGroupItems.Select(i => i.SpotGroupId).ToList())
    ).ToListAsync(cancellationToken);

    var eventsByGroup = activeGroupIds
      .ToDictionary(id => id, _ => new List<SpotGroupEvent>());

    foreach (EventRow ev in eventRows)
    {
      foreach (Guid affected in ev.AffectedSpotGroupIds)
      {
        if (eventsByGroup.TryGetValue(affected, out List<SpotGroupEvent>? list))
        {
          list.Add(new SpotGroupEvent(ev.Id, ev.Name, ev.Description, ev.StartsAt, ev.EndsAt));
        }
      }
    }

    var availabilities = groups
      .Select(g => BuildAvailability(g, totalSpotsByGroup, reservedByGroup, groupsFullyOoo, spotOooByGroup, groupHeldByGroup, eventsByGroup))
      .ToList();

    return new AvailabilityResponse(availabilities);
  }

  private static SpotGroupAvailability BuildAvailability(
    SpotGroupRow group,
    Dictionary<Guid, int> totalSpotsByGroup,
    Dictionary<Guid, int> reservedByGroup,
    HashSet<Guid> groupsFullyOoo,
    Dictionary<Guid, int> spotOooByGroup,
    Dictionary<Guid, int> groupHeldByGroup,
    Dictionary<Guid, List<SpotGroupEvent>> eventsByGroup)
  {
    int totalSpots = totalSpotsByGroup.TryGetValue(group.Id, out int ts) ? ts : 0;
    int reservedCount = reservedByGroup.TryGetValue(group.Id, out int rc) ? rc : 0;
    int spotOooCount = spotOooByGroup.TryGetValue(group.Id, out int sc) ? sc : 0;
    int oooCount = groupsFullyOoo.Contains(group.Id) ? totalSpots : spotOooCount;
    int groupHeldCount = groupHeldByGroup.TryGetValue(group.Id, out int gc) ? gc : 0;
    int occupied = Math.Min(totalSpots, reservedCount + oooCount + groupHeldCount);
    int available = Math.Max(0, totalSpots - occupied);
    List<SpotGroupEvent> events = eventsByGroup.TryGetValue(group.Id, out List<SpotGroupEvent>? evs)
      ? evs
      : [];

    return new SpotGroupAvailability(
      group.Id,
      group.Name,
      group.Capacity,
      totalSpots,
      occupied,
      available,
      group.ImageUrl,
      group.DetailsUrl,
      events);
  }

  private sealed record SpotGroupRow(Guid Id, string Name, uint Capacity, string ImageUrl, string DetailsUrl);
  private sealed record ReservedRow(Guid SpotGroupId, int Count);
  private sealed record SpotOooRow(Guid SpotGroupId, int Count);
  private sealed record GroupHeldRow(Guid SpotGroupId, int Count);
  private sealed record EventRow(
    Guid Id,
    string Name,
    string? Description,
    DateOnly StartsAt,
    DateOnly? EndsAt,
    List<Guid> AffectedSpotGroupIds);
}
