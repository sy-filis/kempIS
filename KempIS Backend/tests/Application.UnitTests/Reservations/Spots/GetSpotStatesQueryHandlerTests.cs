using Application.Reservations.Spots;
using Domain.Common;
using Domain.Operations.OutOfOrders;
using Domain.Operations.SpotGroupOOFItems;
using Domain.Operations.SpotOOFItems;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.ReservationStates;
using Domain.Reservations.SpotGroups;
using Domain.Reservations.Spots;
using SharedKernel;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Application.UnitTests.Reservations.Spots;

public sealed class GetSpotStatesQueryHandlerTests : HandlerTestBase
{
  private static readonly DateTime DefaultNowUtc = new(2026, 5, 3, 12, 0, 0, DateTimeKind.Utc);
  private static DateOnly DefaultToday => DateOnly.FromDateTime(DefaultNowUtc);

  private GetSpotStatesQueryHandler CreateSut()
  {
    Clock.Set(DefaultNowUtc);
    return new GetSpotStatesQueryHandler(Db, Clock);
  }

  private async Task<SpotGroup> SeedSpotGroup()
  {
    SpotGroup g = new()
    {
      Id = Guid.NewGuid(),
      Name = "G",
      Capacity = 10,
      IsActive = true,
      ServiceId = Guid.NewGuid(),
      ImageUrl = "https://example.com/img.jpg",
      DetailsUrl = "https://example.com/details",
    };
    Db.SpotGroups.Add(g);
    await Db.SaveChangesAsync();
    return g;
  }

  private async Task<Spot> SeedSpot(Guid spotGroupId, bool active = true)
  {
    Spot s = new()
    {
      Id = Guid.NewGuid(),
      SpotGroupId = spotGroupId,
      Name = "S",
      IsActive = active,
    };
    Db.Spots.Add(s);
    await Db.SaveChangesAsync();
    return s;
  }

  private async Task<DomainReservation> SeedReservation(DateOnly from, DateOnly to, ReservationState state)
  {
    DomainReservation r = new ReservationBuilder()
      .InState(state)
      .For(from, to)
      .Build();
    Db.Reservations.Add(r);
    await Db.SaveChangesAsync();
    return r;
  }

  private async Task<ReservationSpotItem> SeedRsi(
    Guid reservationId,
    Guid spotGroupId,
    Guid spotId,
    bool returned = false,
    bool hasGivenKey = false)
  {
    ReservationSpotItem rsi = new()
    {
      Id = Guid.NewGuid(),
      ReservationId = reservationId,
      SpotGroupId = spotGroupId,
      SpotId = spotId,
      HasReturnedKeys = returned,
      HasGivenKey = hasGivenKey,
    };
    Db.ReservationSpotItems.Add(rsi);
    await Db.SaveChangesAsync();
    return rsi;
  }

  private async Task<OutOfOrder> SeedDirectOoo(Guid spotId, DateOnly from, DateOnly to)
  {
    OutOfOrder o = new()
    {
      Id = Guid.NewGuid(),
      Period = new DateRange(from, to),
      Reason = "test",
    };
    Db.OutOfOrders.Add(o);
    Db.SpotOofItems.Add(new SpotOofItem { Id = Guid.NewGuid(), SpotId = spotId, OutOfOrderId = o.Id });
    await Db.SaveChangesAsync();
    return o;
  }

  private async Task<OutOfOrder> SeedGroupOoo(Guid spotGroupId, DateOnly from, DateOnly to)
  {
    OutOfOrder o = new()
    {
      Id = Guid.NewGuid(),
      Period = new DateRange(from, to),
      Reason = "test",
    };
    Db.OutOfOrders.Add(o);
    Db.SpotGroupOofItems.Add(new SpotGroupOofItem { Id = Guid.NewGuid(), SpotGroupId = spotGroupId, OutOfOrderId = o.Id });
    await Db.SaveChangesAsync();
    return o;
  }

  [Fact]
  public async Task Empty_ReturnsEmptyList()
  {
    Result<List<SpotStateResponse>> result = await CreateSut().Handle(new GetSpotStatesQuery(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldBeEmpty();
  }

  [Fact]
  public async Task InactiveSpot_IsExcluded()
  {
    SpotGroup g = await SeedSpotGroup();
    await SeedSpot(g.Id, active: false);

    Result<List<SpotStateResponse>> result = await CreateSut().Handle(new GetSpotStatesQuery(), CancellationToken.None);

    result.Value.ShouldBeEmpty();
  }

  [Fact]
  public async Task SpotWithNoReservations_IsUnoccupied()
  {
    SpotGroup g = await SeedSpotGroup();
    Spot s = await SeedSpot(g.Id);

    Result<List<SpotStateResponse>> result = await CreateSut().Handle(new GetSpotStatesQuery(), CancellationToken.None);

    SpotStateResponse row = result.Value.Single();
    row.SpotId.ShouldBe(s.Id);
    row.State.ShouldBe(SpotState.Unoccupied);
    row.DepartureDate.ShouldBeNull();
  }

  [Fact]
  public async Task CheckedIn_DepartingFuture_IsOccupied_WithDepartureDate()
  {
    SpotGroup g = await SeedSpotGroup();
    Spot s = await SeedSpot(g.Id);
    DomainReservation r = await SeedReservation(DefaultToday.AddDays(-1), DefaultToday.AddDays(2), ReservationState.CheckedIn);
    await SeedRsi(r.Id, g.Id, s.Id);

    Result<List<SpotStateResponse>> result = await CreateSut().Handle(new GetSpotStatesQuery(), CancellationToken.None);

    SpotStateResponse row = result.Value.Single();
    row.State.ShouldBe(SpotState.Occupied);
    row.DepartureDate.ShouldBe(DefaultToday.AddDays(2));
  }

  [Fact]
  public async Task CheckedIn_DepartingToday_IsExpectingDeparture_WithDepartureDate()
  {
    SpotGroup g = await SeedSpotGroup();
    Spot s = await SeedSpot(g.Id);
    DomainReservation r = await SeedReservation(DefaultToday.AddDays(-1), DefaultToday, ReservationState.CheckedIn);
    await SeedRsi(r.Id, g.Id, s.Id);

    Result<List<SpotStateResponse>> result = await CreateSut().Handle(new GetSpotStatesQuery(), CancellationToken.None);

    SpotStateResponse row = result.Value.Single();
    row.State.ShouldBe(SpotState.ExpectingDeparture);
    row.DepartureDate.ShouldBe(DefaultToday);
  }

  [Fact]
  public async Task Confirmed_ArrivingToday_IsExpectingArrival()
  {
    SpotGroup g = await SeedSpotGroup();
    Spot s = await SeedSpot(g.Id);
    DomainReservation r = await SeedReservation(DefaultToday, DefaultToday.AddDays(3), ReservationState.Confirmed);
    await SeedRsi(r.Id, g.Id, s.Id);

    Result<List<SpotStateResponse>> result = await CreateSut().Handle(new GetSpotStatesQuery(), CancellationToken.None);

    SpotStateResponse row = result.Value.Single();
    row.State.ShouldBe(SpotState.ExpectingArrival);
    row.DepartureDate.ShouldBeNull();
  }

  [Fact]
  public async Task Confirmed_ArrivingToday_HasGivenKey_IsOccupied()
  {
    SpotGroup g = await SeedSpotGroup();
    Spot s = await SeedSpot(g.Id);
    DomainReservation r = await SeedReservation(DefaultToday, DefaultToday.AddDays(3), ReservationState.Confirmed);
    await SeedRsi(r.Id, g.Id, s.Id, hasGivenKey: true);

    Result<List<SpotStateResponse>> result = await CreateSut().Handle(new GetSpotStatesQuery(), CancellationToken.None);

    SpotStateResponse row = result.Value.Single();
    row.State.ShouldBe(SpotState.Occupied);
    row.DepartureDate.ShouldBe(DefaultToday.AddDays(3));
    row.HasGivenKey.ShouldBeTrue();
  }

  [Fact]
  public async Task Confirmed_LeavingToday_HasGivenKey_IsExpectingDeparture()
  {
    SpotGroup g = await SeedSpotGroup();
    Spot s = await SeedSpot(g.Id);
    DomainReservation r = await SeedReservation(DefaultToday.AddDays(-1), DefaultToday, ReservationState.Confirmed);
    await SeedRsi(r.Id, g.Id, s.Id, hasGivenKey: true);

    Result<List<SpotStateResponse>> result = await CreateSut().Handle(new GetSpotStatesQuery(), CancellationToken.None);

    SpotStateResponse row = result.Value.Single();
    row.State.ShouldBe(SpotState.ExpectingDeparture);
    row.DepartureDate.ShouldBe(DefaultToday);
  }

  [Fact]
  public async Task BackToBack_ConfirmedWithKey_BeatsConfirmedWithout()
  {
    SpotGroup g = await SeedSpotGroup();
    Spot s = await SeedSpot(g.Id);
    DomainReservation occupant = await SeedReservation(DefaultToday.AddDays(-1), DefaultToday, ReservationState.Confirmed);
    DomainReservation incoming = await SeedReservation(DefaultToday, DefaultToday.AddDays(2), ReservationState.Confirmed);
    await SeedRsi(occupant.Id, g.Id, s.Id, hasGivenKey: true);
    await SeedRsi(incoming.Id, g.Id, s.Id);

    Result<List<SpotStateResponse>> result = await CreateSut().Handle(new GetSpotStatesQuery(), CancellationToken.None);

    SpotStateResponse row = result.Value.Single();
    row.State.ShouldBe(SpotState.ExpectingDeparture);
    row.DepartureDate.ShouldBe(DefaultToday);
    row.HasGivenKey.ShouldBeTrue();
  }

  [Fact]
  public async Task Confirmed_ArrivingFutureDay_IsUnoccupied()
  {
    SpotGroup g = await SeedSpotGroup();
    Spot s = await SeedSpot(g.Id);
    DomainReservation r = await SeedReservation(DefaultToday.AddDays(2), DefaultToday.AddDays(5), ReservationState.Confirmed);
    await SeedRsi(r.Id, g.Id, s.Id);

    Result<List<SpotStateResponse>> result = await CreateSut().Handle(new GetSpotStatesQuery(), CancellationToken.None);

    result.Value.Single().State.ShouldBe(SpotState.Unoccupied);
  }

  [Fact]
  public async Task Completed_IsUnoccupied()
  {
    SpotGroup g = await SeedSpotGroup();
    Spot s = await SeedSpot(g.Id);
    DomainReservation r = await SeedReservation(DefaultToday.AddDays(-2), DefaultToday.AddDays(2), ReservationState.Completed);
    await SeedRsi(r.Id, g.Id, s.Id, returned: true);

    Result<List<SpotStateResponse>> result = await CreateSut().Handle(new GetSpotStatesQuery(), CancellationToken.None);

    result.Value.Single().State.ShouldBe(SpotState.Unoccupied);
  }

  [Fact]
  public async Task ReturnedKeys_IsUnoccupied()
  {
    SpotGroup g = await SeedSpotGroup();
    Spot s = await SeedSpot(g.Id);
    DomainReservation r = await SeedReservation(DefaultToday.AddDays(-1), DefaultToday.AddDays(1), ReservationState.CheckedIn);
    await SeedRsi(r.Id, g.Id, s.Id, returned: true);

    Result<List<SpotStateResponse>> result = await CreateSut().Handle(new GetSpotStatesQuery(), CancellationToken.None);

    result.Value.Single().State.ShouldBe(SpotState.Unoccupied);
  }

  [Fact]
  public async Task DirectOoo_OverridesOccupancy()
  {
    SpotGroup g = await SeedSpotGroup();
    Spot s = await SeedSpot(g.Id);
    DomainReservation r = await SeedReservation(DefaultToday.AddDays(-1), DefaultToday.AddDays(2), ReservationState.CheckedIn);
    await SeedRsi(r.Id, g.Id, s.Id);
    await SeedDirectOoo(s.Id, DefaultToday.AddDays(-1), DefaultToday.AddDays(1));

    Result<List<SpotStateResponse>> result = await CreateSut().Handle(new GetSpotStatesQuery(), CancellationToken.None);

    SpotStateResponse row = result.Value.Single();
    row.State.ShouldBe(SpotState.OutOfOrder);
    row.DepartureDate.ShouldBeNull();
  }

  [Fact]
  public async Task GroupOoo_AppliesToSpot()
  {
    SpotGroup g = await SeedSpotGroup();
    _ = await SeedSpot(g.Id);
    await SeedGroupOoo(g.Id, DefaultToday.AddDays(-1), DefaultToday.AddDays(1));

    Result<List<SpotStateResponse>> result = await CreateSut().Handle(new GetSpotStatesQuery(), CancellationToken.None);

    result.Value.Single().State.ShouldBe(SpotState.OutOfOrder);
  }

  [Fact]
  public async Task BackToBack_PrefersCheckedInOverConfirmed()
  {
    SpotGroup g = await SeedSpotGroup();
    Spot s = await SeedSpot(g.Id);
    DomainReservation departing = await SeedReservation(DefaultToday.AddDays(-2), DefaultToday, ReservationState.CheckedIn);
    DomainReservation arriving = await SeedReservation(DefaultToday, DefaultToday.AddDays(3), ReservationState.Confirmed);
    await SeedRsi(departing.Id, g.Id, s.Id);
    await SeedRsi(arriving.Id, g.Id, s.Id);

    Result<List<SpotStateResponse>> result = await CreateSut().Handle(new GetSpotStatesQuery(), CancellationToken.None);

    SpotStateResponse row = result.Value.Single();
    row.State.ShouldBe(SpotState.ExpectingDeparture);
    row.DepartureDate.ShouldBe(DefaultToday);
  }

  [Fact]
  public async Task OooNotCoveringNow_IsIgnored()
  {
    SpotGroup g = await SeedSpotGroup();
    Spot s = await SeedSpot(g.Id);
    await SeedDirectOoo(s.Id, DefaultToday.AddDays(-2), DefaultToday.AddDays(-1));  // ended yesterday

    Result<List<SpotStateResponse>> result = await CreateSut().Handle(new GetSpotStatesQuery(), CancellationToken.None);

    result.Value.Single().State.ShouldBe(SpotState.Unoccupied);
  }

  [Fact]
  public async Task Handle_SetsHasGivenKeyAndIsPaid_ForBoundSpot()
  {
    Clock.Set(DefaultNowUtc);
    var today = DateOnly.FromDateTime(Clock.UtcNow);
    DomainReservation reservation = new ReservationBuilder()
      .InState(ReservationState.CheckedIn)
      .For(today.AddDays(-1), today.AddDays(2))
      .Build();
    Db.Reservations.Add(reservation);

    Spot spot = new()
    {
      Id = Guid.NewGuid(),
      SpotGroupId = Guid.NewGuid(),
      Name = "Cabin 1",
      Description = null,
      IsActive = true,
    };
    Db.Spots.Add(spot);

    var billId = Guid.NewGuid();
    // No Bill row inserted; FK enforcement is OFF in HandlerTestBase.
    ReservationSpotItem spotItem = new()
    {
      Id = Guid.NewGuid(),
      ReservationId = reservation.Id,
      SpotGroupId = spot.SpotGroupId,
      SpotId = spot.Id,
      HasGivenKey = true,
      BillId = billId,
    };
    Db.ReservationSpotItems.Add(spotItem);
    await Db.SaveChangesAsync();

    Result<List<SpotStateResponse>> result = await new GetSpotStatesQueryHandler(Db, Clock)
      .Handle(new GetSpotStatesQuery(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    SpotStateResponse row = result.Value.Single(r => r.SpotId == spot.Id);
    row.HasGivenKey.ShouldBeTrue();
    row.IsPaid.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_ReportsFalseFlags_ForUnoccupiedSpot()
  {
    Clock.Set(DefaultNowUtc);
    Spot spot = new()
    {
      Id = Guid.NewGuid(),
      SpotGroupId = Guid.NewGuid(),
      Name = "Cabin 2",
      Description = null,
      IsActive = true,
    };
    Db.Spots.Add(spot);
    await Db.SaveChangesAsync();

    Result<List<SpotStateResponse>> result = await new GetSpotStatesQueryHandler(Db, Clock)
      .Handle(new GetSpotStatesQuery(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    SpotStateResponse row = result.Value.Single(r => r.SpotId == spot.Id);
    row.State.ShouldBe(SpotState.Unoccupied);
    row.HasGivenKey.ShouldBeFalse();
    row.IsPaid.ShouldBeFalse();
  }
}
