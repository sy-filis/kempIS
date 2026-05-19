using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Reservations.Meals;
using Domain.Reservations;
using Domain.Reservations.Meals;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Queries.GetReservationById;

internal sealed class GetReservationByIdQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetReservationByIdQuery, ReservationDetailResponse>
{
  public async Task<Result<ReservationDetailResponse>> Handle(
    GetReservationByIdQuery query,
    CancellationToken cancellationToken)
  {
    ReservationHeader? header = await context.Reservations
      .AsNoTracking()
      .Where(r => r.Id == query.Id)
      .Select(r => new ReservationHeader(
        r.Id,
        r.Number,
        r.Secret,
        r.State,
        r.Period.From,
        r.Period.To,
        r.ReservationMaker.Name,
        r.ReservationMaker.Surname,
        r.ReservationMaker.Email,
        r.ReservationMaker.Phone,
        r.GroupReservationId,
        r.Note,
        r.CreatedAtUtc,
        r.UpdatedAtUtc,
        r.DisplayName))
      .FirstOrDefaultAsync(cancellationToken);

    if (header is null)
    {
      return Result.Failure<ReservationDetailResponse>(ReservationErrors.NotFound(query.Id));
    }

    List<ReservationDetailGuest> guests = await context.Guests
      .AsNoTracking()
      .Where(g => g.ReservationId == query.Id)
      .OrderBy(g => g.LastName).ThenBy(g => g.FirstName)
      .Select(g => new ReservationDetailGuest(
        g.Id,
        g.BillId,
        g.PaysRecreationFee,
        g.FirstName,
        g.LastName,
        g.NationalityId,
        g.DateOfBirth,
        g.DocumentType,
        g.DocumentNumber,
        g.Address,
        g.ReasonOfStay,
        g.StayDateRange != null ? g.StayDateRange.From : (DateOnly?)null,
        g.StayDateRange != null ? g.StayDateRange.To : (DateOnly?)null,
        g.VisaNumber,
        g.Note,
        g.Scartation,
        g.CheckInAt,
        g.CheckOutAt,
        g.SignaturePng != null,
        g.SignatureCapturedAtUtc,
        g.ReportedAt))
      .ToListAsync(cancellationToken);

    List<ReservationDetailSpotItem> spotItems = await context.ReservationSpotItems
      .AsNoTracking()
      .Where(s => s.ReservationId == query.Id)
      .OrderBy(s => s.SpotGroupId).ThenBy(s => s.SpotId)
      .Select(s => new ReservationDetailSpotItem(
        s.Id,
        s.SpotGroupId,
        s.SpotId,
        s.HasGivenKey,
        s.HasReturnedKeys,
        s.BillId))
      .ToListAsync(cancellationToken);

    List<ReservationDetailServiceItem> serviceItems = await context.ReservationServiceItems
      .AsNoTracking()
      .Where(s => s.ReservationId == query.Id)
      .Select(s => new ReservationDetailServiceItem(
        s.Id,
        s.ServiceId,
        s.Quantity,
        s.RecapSingleQuantity,
        s.RecapDayQuantity))
      .ToListAsync(cancellationToken);

    List<ReservationDetailVehicle> vehicles = await context.Vehicles
      .AsNoTracking()
      .Where(v => v.ReservationId == query.Id)
      .OrderBy(v => v.RegistrationNumber)
      .Select(v => new ReservationDetailVehicle(
        v.Id,
        v.BillId,
        v.ServiceId,
        v.RegistrationNumber))
      .ToListAsync(cancellationToken);

    List<Meal> mealEntities = await context.Meals
      .AsNoTracking()
      .Where(m => m.ReservationId == query.Id)
      .OrderBy(m => m.Date)
      .ToListAsync(cancellationToken);

    List<ReservationDetailMeal> meals = mealEntities.ConvertAll(m => new ReservationDetailMeal(
      m.Date,
      m.Breakfast.ToDto(),
      m.Lunch.ToDto(),
      m.LunchPackage.ToDto(),
      m.Dinner.ToDto()));

    List<ReservationDetailInvoice> invoices = await context.Invoices
      .AsNoTracking()
      .Where(i => i.ReservationId == query.Id)
      .OrderByDescending(i => i.IssuedAt)
      .Select(i => new ReservationDetailInvoice(
        i.Id,
        i.Number,
        i.Status,
        i.IssuedAt,
        i.PaidAt,
        i.LinkedBillId))
      .ToListAsync(cancellationToken);

    var linkedBillIds = invoices
      .Where(i => i.LinkedBillId.HasValue)
      .Select(i => i.LinkedBillId!.Value)
      .ToHashSet();

    List<ReservationDetailBill> bills = await context.Bills
      .AsNoTracking()
      .Where(b => b.ReservationId == query.Id || linkedBillIds.Contains(b.Id))
      .OrderByDescending(b => b.IssuedAtUtc)
      .Select(b => new ReservationDetailBill(
        b.Id,
        b.Number,
        b.Kind,
        b.IssuedAtUtc,
        b.Payment.PaymentType,
        b.Payment.Amount))
      .ToListAsync(cancellationToken);

    List<ReservationDetailAccessCard> accessCards = await context.AccessCards
      .AsNoTracking()
      .Where(c => c.BillId != null
        && context.Bills.Any(b => b.Id == c.BillId && b.ReservationId == query.Id))
      .OrderBy(c => c.IssuedAtUtc)
      .Select(c => new ReservationDetailAccessCard(
        c.Id,
        c.Uid,
        c.Deposit,
        c.IssuedAtUtc))
      .ToListAsync(cancellationToken);

    return new ReservationDetailResponse(
      header.Id,
      header.Number,
      header.Secret,
      header.State,
      header.From,
      header.To,
      header.MakerName,
      header.MakerSurname,
      header.MakerEmail,
      header.MakerPhone,
      header.GroupReservationId,
      header.Note,
      header.CreatedAtUtc,
      header.UpdatedAtUtc,
      guests,
      spotItems,
      serviceItems,
      vehicles,
      meals,
      invoices,
      bills,
      accessCards,
      header.DisplayName);
  }

  private sealed record ReservationHeader(
    Guid Id,
    string Number,
    string Secret,
    Domain.Reservations.ReservationStates.ReservationState State,
    DateOnly From,
    DateOnly To,
    string MakerName,
    string MakerSurname,
    string MakerEmail,
    string MakerPhone,
    Guid? GroupReservationId,
    string? Note,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string? DisplayName);
}
