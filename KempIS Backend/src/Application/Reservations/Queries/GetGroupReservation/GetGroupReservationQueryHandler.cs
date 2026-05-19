using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Reservations;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Queries.GetGroupReservation;

internal sealed class GetGroupReservationQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetGroupReservationQuery, GroupReservationResponse>
{
  public async Task<Result<GroupReservationResponse>> Handle(
    GetGroupReservationQuery query,
    CancellationToken cancellationToken)
  {
    GroupReservationResponse? response = await context.GroupReservations
      .Where(g => g.Id == query.Id)
      .Select(g => new GroupReservationResponse(
        g.Id,
        g.Number,
        g.State.ToString(),
        g.Period.From,
        g.Period.To,
        g.Secret,
        g.OrganizerName,
        g.OrganizerEmail,
        g.OrganizerPhone,
        g.Note,
        g.CreatedAtUtc,
        g.UpdatedAtUtc,
        g.HeldSpots.Select(h => h.SpotId).ToList(),
        g.DisplayName))
      .FirstOrDefaultAsync(cancellationToken);

    if (response is null)
    {
      return Result.Failure<GroupReservationResponse>(GroupReservationErrors.NotFound(query.Id));
    }

    return response;
  }
}
