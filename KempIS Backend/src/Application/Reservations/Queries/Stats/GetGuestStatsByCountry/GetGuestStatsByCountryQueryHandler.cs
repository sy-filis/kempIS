using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Queries.Stats.GetGuestStatsByCountry;

internal sealed class GetGuestStatsByCountryQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetGuestStatsByCountryQuery, GuestStatsByCountryResponse>
{
  public async Task<Result<GuestStatsByCountryResponse>> Handle(
    GetGuestStatsByCountryQuery query,
    CancellationToken cancellationToken)
  {
    List<ProjectedGuest> projected = await context.Guests
      .AsNoTracking()
      .Where(g => g.BillId != null
               && context.Bills.Any(b => b.Id == g.BillId
                                      && b.CheckInAt <= query.To
                                      && b.CheckOutAt >= query.From))
      .Select(g => new ProjectedGuest(
          g.NationalityId,
          g.Nationality!.Alpha2,
          g.Nationality!.Alpha3,
          g.Nationality!.Name,
          g.Nationality!.NameEn,
          context.Bills.Where(b => b.Id == g.BillId).Select(b => b.CheckInAt).First(),
          context.Bills.Where(b => b.Id == g.BillId).Select(b => b.CheckOutAt).First()))
      .ToListAsync(cancellationToken);

    int fromDay = query.From.DayNumber;
    int toExclusive = query.To.DayNumber + 1;

    List<GuestStatsByCountryRow> rows = BuildRows(projected, fromDay, toExclusive);

    int totalGuests = rows.Sum(r => r.GuestCount);
    int totalPersonNights = rows.Sum(r => r.PersonNights);

    return new GuestStatsByCountryResponse(query.From, query.To, totalGuests, totalPersonNights, rows);
  }

  private static List<GuestStatsByCountryRow> BuildRows(
    List<ProjectedGuest> projected,
    int fromDay,
    int toExclusive)
  {
    List<GuestStatsByCountryRow> rows = [];
    foreach (IGrouping<Guid, ProjectedGuest> g in projected
      .GroupBy(p => p.NationalityId)
      .OrderByDescending(g => g.Sum(p => ClampedNights(p, fromDay, toExclusive)))
      .ThenBy(g => g.First().Alpha3, StringComparer.Ordinal))
    {
      int personNights = 0;
      foreach (ProjectedGuest p in g)
      {
        personNights += ClampedNights(p, fromDay, toExclusive);
      }
      ProjectedGuest first = g.First();
      rows.Add(new GuestStatsByCountryRow(
        g.Key, first.Alpha2, first.Alpha3, first.Name, first.NameEn,
        g.Count(), personNights));
    }
    return rows;
  }

  private static int ClampedNights(ProjectedGuest p, int fromDay, int toExclusive) =>
    Math.Max(0,
      Math.Min(p.CheckOut.DayNumber, toExclusive) -
      Math.Max(p.CheckIn.DayNumber, fromDay));

  private readonly record struct ProjectedGuest(
    Guid NationalityId,
    string Alpha2,
    string Alpha3,
    string Name,
    string NameEn,
    DateOnly CheckIn,
    DateOnly CheckOut);
}
