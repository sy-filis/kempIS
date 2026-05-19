using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Reservations.Meals;
using Domain.Reservations;
using Domain.Reservations.Meals;
using Domain.Reservations.Reservations;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Queries.GetReservationForGuest;

internal sealed class GetReservationForGuestQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetReservationForGuestQuery, ReservationForGuestResponse>
{
  public async Task<Result<ReservationForGuestResponse>> Handle(
    GetReservationForGuestQuery query,
    CancellationToken cancellationToken)
  {
    Reservation? reservation = await context.Reservations
      .FirstOrDefaultAsync(r => r.Id == query.Id, cancellationToken);

    if (reservation is null)
    {
      return Result.Failure<ReservationForGuestResponse>(ReservationErrors.NotFound(query.Id));
    }

    if (!string.Equals(reservation.Secret, query.Secret, StringComparison.Ordinal))
    {
      return Result.Failure<ReservationForGuestResponse>(ReservationErrors.SecretInvalid);
    }

    var rawSpotItems = await context.ReservationSpotItems
      .AsNoTracking()
      .Where(i => i.ReservationId == reservation.Id)
      .Select(i => new { i.SpotGroupId, i.SpotId })
      .ToListAsync(cancellationToken);

    var spotGroupIds = rawSpotItems.Select(i => i.SpotGroupId).Distinct().ToList();
    var spotIds = rawSpotItems.Where(i => i.SpotId.HasValue).Select(i => i.SpotId!.Value).Distinct().ToList();

    Dictionary<Guid, (string Name, Guid ServiceId)> spotGroupLookup = await context.SpotGroups
      .AsNoTracking()
      .Where(sg => spotGroupIds.Contains(sg.Id))
      .Select(sg => new { sg.Id, sg.Name, sg.ServiceId })
      .ToDictionaryAsync(sg => sg.Id, sg => (sg.Name, sg.ServiceId), cancellationToken);

    Dictionary<Guid, string> spotNameLookup = await context.Spots
      .AsNoTracking()
      .Where(s => spotIds.Contains(s.Id))
      .Select(s => new { s.Id, s.Name })
      .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

    var groupSpotsLookup = (await context.Spots
        .AsNoTracking()
        .Where(s => spotGroupIds.Contains(s.SpotGroupId) && s.IsActive)
        .Select(s => new { s.Id, s.Name, s.SpotGroupId })
        .ToListAsync(cancellationToken))
      .GroupBy(s => s.SpotGroupId)
      .ToDictionary(
        g => g.Key,
        g => g.OrderBy(x => x.Name, StringComparer.Ordinal)
              .Select(x => new ReservationForGuestGroupSpot(x.Id, x.Name))
              .ToList());

    var serviceIds = spotGroupLookup.Values.Select(v => v.ServiceId).Distinct().ToList();

    var serviceTextLookup = (await context.ServiceTexts
        .AsNoTracking()
        .Where(st => serviceIds.Contains(st.ServiceId))
        .Select(st => new { st.ServiceId, st.LanguageId, st.PrintText })
        .ToListAsync(cancellationToken))
      .GroupBy(st => st.ServiceId)
      .ToDictionary(
        g => g.Key,
        g => g.OrderBy(x => x.LanguageId)
              .Select(x => new ReservationForGuestServiceText(x.LanguageId, x.PrintText))
              .ToList());

    List<ReservationForGuestSpotItem> spotItems = [.. rawSpotItems
      .Select(i =>
      {
        spotGroupLookup.TryGetValue(i.SpotGroupId, out (string Name, Guid ServiceId) sg);
        string? spotName = i.SpotId.HasValue && spotNameLookup.TryGetValue(i.SpotId.Value, out string? n) ? n : null;
        IReadOnlyList<ReservationForGuestGroupSpot> groupSpots =
          groupSpotsLookup.TryGetValue(i.SpotGroupId, out List<ReservationForGuestGroupSpot>? gs)
            ? gs
            : [];
        IReadOnlyList<ReservationForGuestServiceText> texts =
          serviceTextLookup.TryGetValue(sg.ServiceId, out List<ReservationForGuestServiceText>? list)
            ? list
            : [];
        return new ReservationForGuestSpotItem(i.SpotGroupId, sg.Name ?? string.Empty, i.SpotId, spotName, groupSpots, texts);
      })
      .OrderBy(s => s.SpotGroupName, StringComparer.Ordinal)
      .ThenBy(s => s.SpotName, StringComparer.Ordinal)];

    List<Meal> mealEntities = await context.Meals
      .AsNoTracking()
      .Where(m => m.ReservationId == reservation.Id)
      .OrderBy(m => m.Date)
      .ToListAsync(cancellationToken);

    List<ReservationForGuestMeal> meals = mealEntities.ConvertAll(m => new ReservationForGuestMeal(
      m.Date,
      m.Breakfast.ToDto(),
      m.Lunch.ToDto(),
      m.LunchPackage.ToDto(),
      m.Dinner.ToDto()));

    List<ReservationForGuestBill> bills = await context.Bills
      .AsNoTracking()
      .Where(b => b.ReservationId == reservation.Id)
      .OrderByDescending(b => b.IssuedAtUtc)
      .Select(b => new ReservationForGuestBill(
        b.Id,
        b.Number,
        b.Kind,
        b.Payment.Amount))
      .ToListAsync(cancellationToken);

    return new ReservationForGuestResponse(
      reservation.Id,
      reservation.Number,
      reservation.State.ToString(),
      reservation.Period.From,
      reservation.Period.To,
      reservation.ReservationMaker.Name,
      reservation.ReservationMaker.Surname,
      reservation.Note,
      reservation.GroupReservationId,
      spotItems,
      meals,
      bills);
  }
}
