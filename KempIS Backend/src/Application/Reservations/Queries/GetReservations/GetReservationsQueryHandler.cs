using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Queries.GetReservations;

internal sealed class GetReservationsQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetReservationsQuery, List<ReservationResponse>>
{
  public async Task<Result<List<ReservationResponse>>> Handle(
    GetReservationsQuery query,
    CancellationToken cancellationToken)
  {
    var reservations = await context.Reservations
      .Where(r => query.From == null || r.Period.To >= query.From)
      .Where(r => query.To == null || r.Period.From <= query.To)
      .Where(r => query.Status == null || r.State == query.Status)
      .Select(r => new
      {
        r.Id,
        r.Number,
        MakerName = r.ReservationMaker.Name,
        MakerSurname = r.ReservationMaker.Surname,
        MakerEmail = r.ReservationMaker.Email,
        MakerPhone = r.ReservationMaker.Phone,
        r.GroupReservationId,
        r.Period.From,
        r.Period.To,
        r.State,
        r.CreatedAtUtc,
        r.UpdatedAtUtc,
        r.Note,
        r.DisplayName,
      })
      .ToListAsync(cancellationToken);

    HashSet<Guid> reservationIds = [.. reservations.Select(r => r.Id)];

    var spotItems = await context.ReservationSpotItems
      .Where(rsi => reservationIds.Contains(rsi.ReservationId) && rsi.SpotId != null)
      .Select(rsi => new { rsi.ReservationId, SpotId = rsi.SpotId!.Value })
      .ToListAsync(cancellationToken);

    ILookup<Guid, Guid> spotItemsByReservation = spotItems.ToLookup(x => x.ReservationId, x => x.SpotId);

    return reservations
      .Select(r => new ReservationResponse(
        r.Id,
        r.Number,
        r.MakerName,
        r.MakerSurname,
        r.MakerEmail,
        r.MakerPhone,
        r.GroupReservationId,
        r.From,
        r.To,
        r.State,
        r.CreatedAtUtc,
        r.UpdatedAtUtc,
        r.Note,
        [.. spotItemsByReservation[r.Id]],
        r.DisplayName))
      .ToList();
  }
}
