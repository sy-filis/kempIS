using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Queries.GetGroupReservations;

internal sealed class GetGroupReservationsQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetGroupReservationsQuery, List<GroupReservationListItemResponse>>
{
  public async Task<Result<List<GroupReservationListItemResponse>>> Handle(
    GetGroupReservationsQuery query,
    CancellationToken cancellationToken)
  {
    List<GroupReservationListItemResponse> items = await context.GroupReservations
      .Where(g => g.Period.From <= query.To && g.Period.To >= query.From)
      .Where(g => query.State == null || g.State == query.State)
      .OrderBy(g => g.Period.From)
      .ThenBy(g => g.CreatedAtUtc)
      .Select(g => new GroupReservationListItemResponse(
        g.Id,
        g.Number,
        g.State.ToString(),
        g.Period.From,
        g.Period.To,
        g.OrganizerName,
        g.OrganizerEmail,
        g.OrganizerPhone,
        g.HeldSpots.Select(h => h.SpotId).ToList(),
        g.CreatedAtUtc,
        g.DisplayName))
      .ToListAsync(cancellationToken);

    return items;
  }
}
