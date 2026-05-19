using Application.Reservations.Queries.Stats.GetOccupancyStats;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.ReservationStates;
using SharedKernel;

namespace Application.UnitTests.Reservations.Queries.Stats.GetOccupancyStats;

public sealed class GetOccupancyStatsQueryHandlerTests : HandlerTestBase
{
  private static readonly DateOnly From = new(2026, 7, 1);
  private static readonly DateOnly To = new(2026, 7, 10);

  private GetOccupancyStatsQueryHandler CreateSut() => new(Db);

  private static ReservationSpotItem MakeItem(Guid reservationId, Guid spotGroupId, Guid? spotId = null) => new()
  {
    Id = Guid.NewGuid(),
    ReservationId = reservationId,
    SpotGroupId = spotGroupId,
    SpotId = spotId,
    HasGivenKey = false,
    HasReturnedKeys = false,
  };

  [Fact]
  public async Task Handle_EmptyDb_ReturnsNightsInRangeAndZeros()
  {
    Result<OccupancyStatsResponse> result =
      await CreateSut().Handle(new GetOccupancyStatsQuery(From, To), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.NightsInRange.ShouldBe(10);
    result.Value.TotalOccupiedSpotNights.ShouldBe(0);
    result.Value.TotalCapacitySpotNights.ShouldBe(0);
    result.Value.Groups.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_SingleReservationFullyInsideRange_ContributesFullStay()
  {
    var sgId = Guid.NewGuid();
    Db.SpotGroups.Add(new SpotGroupBuilder().WithId(sgId).WithName("Mobile").WithCapacity(5).Build());
    Domain.Reservations.Reservations.Reservation r = new ReservationBuilder()
      .For(new DateOnly(2026, 7, 3), new DateOnly(2026, 7, 7))
      .InState(ReservationState.Confirmed)
      .Build();
    Db.Reservations.Add(r);
    Db.ReservationSpotItems.Add(MakeItem(r.Id, sgId));
    await Db.SaveChangesAsync();

    Result<OccupancyStatsResponse> result =
      await CreateSut().Handle(new GetOccupancyStatsQuery(From, To), CancellationToken.None);

    result.Value.Groups.Count.ShouldBe(1);
    result.Value.Groups[0].OccupiedSpotNights.ShouldBe(4);  // 7-3
    result.Value.Groups[0].CapacitySpotNights.ShouldBe(50); // 5 × 10
    result.Value.TotalOccupiedSpotNights.ShouldBe(4);
  }

  [Fact]
  public async Task Handle_ReservationSpansFromBoundary_Clamps()
  {
    var sgId = Guid.NewGuid();
    Db.SpotGroups.Add(new SpotGroupBuilder().WithId(sgId).WithName("Mobile").WithCapacity(5).Build());
    Domain.Reservations.Reservations.Reservation r = new ReservationBuilder()
      .For(new DateOnly(2026, 6, 28), new DateOnly(2026, 7, 3))
      .InState(ReservationState.Confirmed)
      .Build();
    Db.Reservations.Add(r);
    Db.ReservationSpotItems.Add(MakeItem(r.Id, sgId));
    await Db.SaveChangesAsync();

    Result<OccupancyStatsResponse> result =
      await CreateSut().Handle(new GetOccupancyStatsQuery(From, To), CancellationToken.None);

    result.Value.Groups[0].OccupiedSpotNights.ShouldBe(2);  // 7/1, 7/2 (period 7/3 is checkout-exclusive)
  }

  [Fact]
  public async Task Handle_ReservationSpansToBoundary_Clamps()
  {
    var sgId = Guid.NewGuid();
    Db.SpotGroups.Add(new SpotGroupBuilder().WithId(sgId).WithName("Mobile").WithCapacity(5).Build());
    Domain.Reservations.Reservations.Reservation r = new ReservationBuilder()
      .For(new DateOnly(2026, 7, 8), new DateOnly(2026, 7, 15))  // extends past To=7/10
      .InState(ReservationState.Confirmed)
      .Build();
    Db.Reservations.Add(r);
    Db.ReservationSpotItems.Add(MakeItem(r.Id, sgId));
    await Db.SaveChangesAsync();

    // 7/8, 7/9, 7/10 → 3 nights (To=7/10 inclusive last night; toExclusive=7/11)
    Result<OccupancyStatsResponse> result =
      await CreateSut().Handle(new GetOccupancyStatsQuery(From, To), CancellationToken.None);

    result.Value.Groups[0].OccupiedSpotNights.ShouldBe(3);
  }

  [Fact]
  public async Task Handle_CreatedAndCancelled_AreExcluded()
  {
    var sgId = Guid.NewGuid();
    Db.SpotGroups.Add(new SpotGroupBuilder().WithId(sgId).WithName("Mobile").WithCapacity(5).Build());
    Domain.Reservations.Reservations.Reservation created = new ReservationBuilder()
      .For(new DateOnly(2026, 7, 3), new DateOnly(2026, 7, 7))
      .InState(ReservationState.Created).Build();
    Domain.Reservations.Reservations.Reservation cancelled = new ReservationBuilder()
      .For(new DateOnly(2026, 7, 3), new DateOnly(2026, 7, 7))
      .InState(ReservationState.Cancelled).Build();
    Db.Reservations.AddRange(created, cancelled);
    Db.ReservationSpotItems.Add(MakeItem(created.Id, sgId));
    Db.ReservationSpotItems.Add(MakeItem(cancelled.Id, sgId));
    await Db.SaveChangesAsync();

    Result<OccupancyStatsResponse> result =
      await CreateSut().Handle(new GetOccupancyStatsQuery(From, To), CancellationToken.None);

    result.Value.Groups.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_AllActiveStates_AreCounted()
  {
    var sgId = Guid.NewGuid();
    Db.SpotGroups.Add(new SpotGroupBuilder().WithId(sgId).WithName("Mobile").WithCapacity(5).Build());
    foreach (ReservationState state in new[] { ReservationState.Confirmed, ReservationState.CheckedIn, ReservationState.Completed })
    {
      Domain.Reservations.Reservations.Reservation r = new ReservationBuilder()
        .WithId(Guid.NewGuid())
        .For(new DateOnly(2026, 7, 3), new DateOnly(2026, 7, 5))
        .InState(state).Build();
      Db.Reservations.Add(r);
      Db.ReservationSpotItems.Add(MakeItem(r.Id, sgId));
    }
    await Db.SaveChangesAsync();

    Result<OccupancyStatsResponse> result =
      await CreateSut().Handle(new GetOccupancyStatsQuery(From, To), CancellationToken.None);

    result.Value.Groups[0].OccupiedSpotNights.ShouldBe(6);  // 3 × 2 nights
  }

  [Fact]
  public async Task Handle_NullSpotId_StillContributes()
  {
    var sgId = Guid.NewGuid();
    Db.SpotGroups.Add(new SpotGroupBuilder().WithId(sgId).WithName("Mobile").WithCapacity(5).Build());
    Domain.Reservations.Reservations.Reservation r = new ReservationBuilder()
      .For(new DateOnly(2026, 7, 3), new DateOnly(2026, 7, 5))
      .InState(ReservationState.Confirmed).Build();
    Db.Reservations.Add(r);
    Db.ReservationSpotItems.Add(MakeItem(r.Id, sgId, spotId: null));
    await Db.SaveChangesAsync();

    Result<OccupancyStatsResponse> result =
      await CreateSut().Handle(new GetOccupancyStatsQuery(From, To), CancellationToken.None);

    result.Value.Groups[0].OccupiedSpotNights.ShouldBe(2);
  }

  [Fact]
  public async Task Handle_InactiveSpotGroupWithOccupancy_IsIncluded()
  {
    var sgId = Guid.NewGuid();
    Db.SpotGroups.Add(new SpotGroupBuilder().WithId(sgId).WithName("Retired").WithCapacity(3).Inactive().Build());
    Domain.Reservations.Reservations.Reservation r = new ReservationBuilder()
      .For(new DateOnly(2026, 7, 3), new DateOnly(2026, 7, 5))
      .InState(ReservationState.Confirmed).Build();
    Db.Reservations.Add(r);
    Db.ReservationSpotItems.Add(MakeItem(r.Id, sgId));
    await Db.SaveChangesAsync();

    Result<OccupancyStatsResponse> result =
      await CreateSut().Handle(new GetOccupancyStatsQuery(From, To), CancellationToken.None);

    result.Value.Groups[0].IsActive.ShouldBeFalse();
  }

  [Fact]
  public async Task Handle_ZeroCapacity_OccupancyPercentIsZero()
  {
    var sgId = Guid.NewGuid();
    Db.SpotGroups.Add(new SpotGroupBuilder().WithId(sgId).WithName("Edge").WithCapacity(0).Build());
    Domain.Reservations.Reservations.Reservation r = new ReservationBuilder()
      .For(new DateOnly(2026, 7, 3), new DateOnly(2026, 7, 5))
      .InState(ReservationState.Confirmed).Build();
    Db.Reservations.Add(r);
    Db.ReservationSpotItems.Add(MakeItem(r.Id, sgId));
    await Db.SaveChangesAsync();

    Result<OccupancyStatsResponse> result =
      await CreateSut().Handle(new GetOccupancyStatsQuery(From, To), CancellationToken.None);

    result.Value.Groups[0].OccupancyPercent.ShouldBe(0m);
  }

  [Fact]
  public async Task Handle_OccupancyPercentRoundedToOneDecimal()
  {
    var sgId = Guid.NewGuid();
    Db.SpotGroups.Add(new SpotGroupBuilder().WithId(sgId).WithName("M").WithCapacity(3).Build());
    Domain.Reservations.Reservations.Reservation r = new ReservationBuilder()
      .For(new DateOnly(2026, 7, 3), new DateOnly(2026, 7, 5))
      .InState(ReservationState.Confirmed).Build();
    Db.Reservations.Add(r);
    Db.ReservationSpotItems.Add(MakeItem(r.Id, sgId));
    await Db.SaveChangesAsync();

    // 2 occupied / (3 × 10 = 30) capacity = 6.666..% → 6.7
    Result<OccupancyStatsResponse> result =
      await CreateSut().Handle(new GetOccupancyStatsQuery(From, To), CancellationToken.None);

    result.Value.Groups[0].OccupancyPercent.ShouldBe(6.7m);
  }
}
