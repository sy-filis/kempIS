using Application.Reservations.Queries.GetAvailability;
using Domain.Operations.Events;
using Domain.Operations.OutOfOrders;
using Domain.Operations.SpotGroupOOFItems;
using Domain.Operations.SpotOOFItems;
using Domain.Reservations.GroupReservations;
using Domain.Reservations.ReservationStates;
using SharedKernel;
using DomainReservation = Domain.Reservations.Reservations.Reservation;
using DomainReservationSpotItem = Domain.Reservations.ReservationSpotItems.ReservationSpotItem;
using DomainSpot = Domain.Reservations.Spots.Spot;
using DomainSpotGroup = Domain.Reservations.SpotGroups.SpotGroup;

namespace Application.UnitTests.Reservations.Queries.GetAvailability;

public sealed class GetAvailabilityQueryHandlerTests : HandlerTestBase
{
  private static readonly DateOnly QFrom = new(2026, 7, 10);
  private static readonly DateOnly QTo = new(2026, 7, 15);

  private GetAvailabilityQueryHandler CreateSut() => new(Db);

  private static GetAvailabilityQuery Query(
      Guid? groupReservationId = null, string? secret = null)
      => new(QFrom, QTo, groupReservationId, secret);

  private async Task<DomainSpotGroup> SeedGroup(uint capacity, bool isActive = true, string name = "G")
  {
    DomainSpotGroup group = new SpotGroupBuilder()
      .WithCapacity(capacity)
      .WithName(name)
      .Build();
    if (!isActive)
    {
      group.IsActive = false;
    }
    Db.SpotGroups.Add(group);
    await Db.SaveChangesAsync();
    return group;
  }

  private async Task<DomainSpot> SeedSpot(Guid groupId)
  {
    DomainSpot spot = new SpotBuilder().InGroup(groupId).Build();
    Db.Spots.Add(spot);
    await Db.SaveChangesAsync();
    return spot;
  }

  private async Task SeedSpots(Guid groupId, int count)
  {
    for (int i = 0; i < count; i++)
    {
      await SeedSpot(groupId);
    }
  }

  private async Task SeedReservationWithSpot(
      Guid spotGroupId,
      Guid spotId,
      DateOnly from,
      DateOnly to,
      ReservationState state)
  {
    DomainReservation r = new ReservationBuilder()
      .InState(state)
      .For(from, to)
      .Build();
    Db.Reservations.Add(r);
    Db.ReservationSpotItems.Add(new DomainReservationSpotItem
    {
      Id = Guid.NewGuid(),
      ReservationId = r.Id,
      SpotGroupId = spotGroupId,
      SpotId = spotId,
    });
    await Db.SaveChangesAsync();
  }

  private async Task SeedSpotOoo(Guid spotId, DateOnly from, DateOnly to)
  {
    OutOfOrder oof = new OutOfOrderBuilder().Between(from, to).Build();
    Db.OutOfOrders.Add(oof);
    Db.SpotOofItems.Add(new SpotOofItem { Id = Guid.NewGuid(), SpotId = spotId, OutOfOrderId = oof.Id });
    await Db.SaveChangesAsync();
  }

  private async Task SeedGroupOoo(Guid spotGroupId, DateOnly from, DateOnly to)
  {
    OutOfOrder oof = new OutOfOrderBuilder().Between(from, to).Build();
    Db.OutOfOrders.Add(oof);
    Db.SpotGroupOofItems.Add(new SpotGroupOofItem
    {
      Id = Guid.NewGuid(),
      SpotGroupId = spotGroupId,
      OutOfOrderId = oof.Id,
    });
    await Db.SaveChangesAsync();
  }

  private async Task<GroupReservation> SeedGroupReservation(
      DateOnly from,
      DateOnly to,
      GroupReservationState state = GroupReservationState.Confirmed,
      string secret = "g-secret",
      IReadOnlyList<Guid>? heldSpotIds = null)
  {
    GroupReservation g = new GroupReservationBuilder()
      .For(from, to)
      .InState(state)
      .WithSecret(secret)
      .HoldingSpots([.. heldSpotIds ?? []])
      .Build();
    Db.GroupReservations.Add(g);
    await Db.SaveChangesAsync();
    return g;
  }

  private async Task SeedEvent(
      string name,
      DateOnly startsAt,
      DateOnly? endsAt,
      IReadOnlyList<Guid> spotGroupIds)
  {
    Event ev = new()
    {
      Id = Guid.NewGuid(),
      Name = name,
      Description = null,
      StartsAt = startsAt,
      EndsAt = endsAt,
      SpotGroupItems = [.. spotGroupIds.Select(gid => new EventSpotGroupItem
      {
        Id = Guid.NewGuid(),
        SpotGroupId = gid,
      })],
    };
    Db.Events.Add(ev);
    await Db.SaveChangesAsync();
  }

  [Fact]
  public async Task Handle_NoReservationsNoOoo_AllCapacityAvailable()
  {
    DomainSpotGroup a = await SeedGroup(5, name: "A");
    DomainSpotGroup b = await SeedGroup(3, name: "B");
    await SeedSpots(a.Id, 5);
    await SeedSpots(b.Id, 3);

    Result<AvailabilityResponse> result = await CreateSut().Handle(Query(), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.SpotGroups.Count.ShouldBe(2);
    SpotGroupAvailability rowA = result.Value.SpotGroups.Single(g => g.SpotGroupId == a.Id);
    rowA.Capacity.ShouldBe(5u);
    rowA.Occupied.ShouldBe(0);
    rowA.Available.ShouldBe(5);
    result.Value.SpotGroups.Single(g => g.SpotGroupId == b.Id).Available.ShouldBe(3);
  }

  [Fact]
  public async Task Handle_InactiveGroups_AreExcludedFromResponse()
  {
    DomainSpotGroup active = await SeedGroup(5);
    await SeedGroup(5, isActive: false);

    Result<AvailabilityResponse> result = await CreateSut().Handle(Query(), CancellationToken.None);

    result.Value.SpotGroups.Count.ShouldBe(1);
    result.Value.SpotGroups[0].SpotGroupId.ShouldBe(active.Id);
  }

  [Theory]
  [InlineData(ReservationState.Confirmed, 1)]
  [InlineData(ReservationState.CheckedIn, 1)]
  [InlineData(ReservationState.Created, 0)]
  [InlineData(ReservationState.Cancelled, 0)]
  [InlineData(ReservationState.Completed, 0)]
  public async Task Handle_OnlyConfirmedAndCheckedInCountAsReserved(ReservationState state, int expectedOccupied)
  {
    DomainSpotGroup g = await SeedGroup(5);
    DomainSpot spot = await SeedSpot(g.Id);
    await SeedSpots(g.Id, 4);
    await SeedReservationWithSpot(g.Id, spot.Id, QFrom, QTo, state);

    Result<AvailabilityResponse> result = await CreateSut().Handle(Query(), CancellationToken.None);

    SpotGroupAvailability row = result.Value.SpotGroups.Single();
    row.Occupied.ShouldBe(expectedOccupied);
    row.Available.ShouldBe(5 - expectedOccupied);
  }

  [Fact]
  public async Task Handle_SpotLevelOoo_CountedAsOccupied()
  {
    DomainSpotGroup g = await SeedGroup(5);
    DomainSpot s1 = await SeedSpot(g.Id);
    DomainSpot s2 = await SeedSpot(g.Id);
    await SeedSpotOoo(s1.Id, QFrom, QTo);
    await SeedSpotOoo(s2.Id, QFrom, QTo);

    Result<AvailabilityResponse> result = await CreateSut().Handle(Query(), CancellationToken.None);

    result.Value.SpotGroups.Single().Occupied.ShouldBe(2);
  }

  [Fact]
  public async Task Handle_GroupLevelOoo_OverridesSpotOooWithFullCapacity()
  {
    DomainSpotGroup g = await SeedGroup(5);
    await SeedSpots(g.Id, 5);
    await SeedGroupOoo(g.Id, QFrom, QTo);

    Result<AvailabilityResponse> result = await CreateSut().Handle(Query(), CancellationToken.None);

    SpotGroupAvailability row = result.Value.SpotGroups.Single();
    row.Occupied.ShouldBe(5);
    row.Available.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_GroupHeldByActiveGroupReservation_CountedAsOccupied()
  {
    DomainSpotGroup g = await SeedGroup(5);
    DomainSpot s1 = await SeedSpot(g.Id);
    DomainSpot s2 = await SeedSpot(g.Id);
    await SeedGroupReservation(QFrom, QTo, heldSpotIds: [s1.Id, s2.Id]);

    Result<AvailabilityResponse> result = await CreateSut().Handle(Query(), CancellationToken.None);

    result.Value.SpotGroups.Single().Occupied.ShouldBe(2);
  }

  [Fact]
  public async Task Handle_AllowedGroupWithValidSecret_ExcludesOwnHeldSpotsFromOccupancy()
  {
    DomainSpotGroup g = await SeedGroup(5);
    DomainSpot s1 = await SeedSpot(g.Id);
    DomainSpot s2 = await SeedSpot(g.Id);
    GroupReservation gr = await SeedGroupReservation(
        QFrom, QTo, secret: "magic", heldSpotIds: [s1.Id, s2.Id]);

    Result<AvailabilityResponse> result = await CreateSut().Handle(
        Query(groupReservationId: gr.Id, secret: "magic"), CancellationToken.None);

    result.Value.SpotGroups.Single().Occupied.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_InvalidSecret_GroupHeldStillCounted()
  {
    DomainSpotGroup g = await SeedGroup(5);
    DomainSpot s1 = await SeedSpot(g.Id);
    GroupReservation gr = await SeedGroupReservation(
        QFrom, QTo, secret: "real", heldSpotIds: [s1.Id]);

    Result<AvailabilityResponse> result = await CreateSut().Handle(
        Query(groupReservationId: gr.Id, secret: "wrong"), CancellationToken.None);

    result.Value.SpotGroups.Single().Occupied.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_CanceledGroupReservation_NotCounted()
  {
    DomainSpotGroup g = await SeedGroup(5);
    DomainSpot s1 = await SeedSpot(g.Id);
    await SeedGroupReservation(
        QFrom, QTo, state: GroupReservationState.Canceled, heldSpotIds: [s1.Id]);

    Result<AvailabilityResponse> result = await CreateSut().Handle(Query(), CancellationToken.None);

    result.Value.SpotGroups.Single().Occupied.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_OccupiedCappedAtTotalSpots_AvailableNeverNegative()
  {
    DomainSpotGroup g = await SeedGroup(5);
    // Seed exactly totalSpots=5; reservations + group-held overlap to push the
    // raw sum above totalSpots and verify the cap.
    var spots = new DomainSpot[5];
    for (int i = 0; i < 5; i++)
    {
      spots[i] = await SeedSpot(g.Id);
    }
    for (int i = 0; i < 4; i++)
    {
      await SeedReservationWithSpot(g.Id, spots[i].Id, QFrom, QTo, ReservationState.Confirmed);
    }
    // 3 held by group reservation; 2 of them are spots already reserved.
    await SeedGroupReservation(QFrom, QTo,
        heldSpotIds: [spots[0].Id, spots[1].Id, spots[4].Id]);

    Result<AvailabilityResponse> result = await CreateSut().Handle(Query(), CancellationToken.None);

    SpotGroupAvailability row = result.Value.SpotGroups.Single();
    row.Occupied.ShouldBe(5);
    row.Available.ShouldBe(0);
  }

  [Fact]
  public async Task Handle_ReservationEndingOnQueryFrom_CountedViaInclusiveBoundary()
  {
    DomainSpotGroup g = await SeedGroup(1);
    DomainSpot s = await SeedSpot(g.Id);
    // reservation ends on QFrom
    await SeedReservationWithSpot(g.Id, s.Id,
        new DateOnly(2026, 7, 5), QFrom, ReservationState.Confirmed);

    Result<AvailabilityResponse> result = await CreateSut().Handle(Query(), CancellationToken.None);

    result.Value.SpotGroups.Single().Occupied.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_ReservationStartingOnQueryTo_CountedViaInclusiveBoundary()
  {
    DomainSpotGroup g = await SeedGroup(1);
    DomainSpot s = await SeedSpot(g.Id);
    await SeedReservationWithSpot(g.Id, s.Id,
        QTo, new DateOnly(2026, 7, 20), ReservationState.Confirmed);

    Result<AvailabilityResponse> result = await CreateSut().Handle(Query(), CancellationToken.None);

    result.Value.SpotGroups.Single().Occupied.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_Event_AffectedSpotGroupsFilteredToActiveOnly()
  {
    DomainSpotGroup active = await SeedGroup(5);
    await SeedGroup(5, isActive: false);
    await SeedEvent("Festival",
        QFrom,
        QTo,
        [active.Id]);

    Result<AvailabilityResponse> result = await CreateSut().Handle(Query(), CancellationToken.None);

    SpotGroupAvailability row = result.Value.SpotGroups.Single();
    row.SpotGroupId.ShouldBe(active.Id);
    row.Events.Single().Name.ShouldBe("Festival");
  }

  [Fact]
  public async Task Handle_EventOnlyHitsInactiveGroups_Suppressed()
  {
    DomainSpotGroup inactive = await SeedGroup(5, isActive: false);
    await SeedEvent("InternalOnly",
        QFrom,
        QTo,
        [inactive.Id]);

    Result<AvailabilityResponse> result = await CreateSut().Handle(Query(), CancellationToken.None);

    result.Value.SpotGroups.SelectMany(g => g.Events).ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_OpenEndedEventStartingBeforeWindow_Included()
  {
    DomainSpotGroup g = await SeedGroup(5);
    await SeedEvent("Ongoing",
        startsAt: new DateOnly(2026, 6, 1),
        endsAt: null,
        spotGroupIds: [g.Id]);

    Result<AvailabilityResponse> result = await CreateSut().Handle(Query(), CancellationToken.None);

    SpotGroupEvent ev = result.Value.SpotGroups.Single().Events.Single();
    ev.Name.ShouldBe("Ongoing");
    ev.EndsAt.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_EventStartingAfterWindow_Excluded()
  {
    DomainSpotGroup g = await SeedGroup(5);
    await SeedEvent("Future",
        startsAt: new DateOnly(2026, 8, 1),
        endsAt: new DateOnly(2026, 8, 5),
        spotGroupIds: [g.Id]);

    Result<AvailabilityResponse> result = await CreateSut().Handle(Query(), CancellationToken.None);

    result.Value.SpotGroups.Single().Events.ShouldBeEmpty();
  }
}
