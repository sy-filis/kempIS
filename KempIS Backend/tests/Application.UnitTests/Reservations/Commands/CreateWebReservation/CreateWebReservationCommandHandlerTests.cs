using Application.Abstractions.Reservations;
using Application.Reservations.Commands.CreateWebReservation;
using Domain.Operations.OutOfOrders;
using Domain.Operations.SpotGroupOOFItems;
using Domain.Operations.SpotOOFItems;
using Domain.Reservations;
using Domain.Reservations.GroupReservations;
using Domain.Reservations.ReservationStates;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.Data.Sqlite;
using SharedKernel;
using DomainReservation = Domain.Reservations.Reservations.Reservation;
using DomainReservationSpotItem = Domain.Reservations.ReservationSpotItems.ReservationSpotItem;
using DomainSpot = Domain.Reservations.Spots.Spot;
using DomainSpotGroup = Domain.Reservations.SpotGroups.SpotGroup;

namespace Application.UnitTests.Reservations.Commands.CreateWebReservation;

public sealed class CreateWebReservationCommandHandlerTests : HandlerTestBase
{
  private static readonly DateOnly QFrom = new(2026, 7, 10);
  private static readonly DateOnly QTo = new(2026, 7, 15);

  private readonly CapturingDomainEventsDispatcher _dispatcher = new();
  private readonly IReservationNumberGenerator _numberGenerator = StubNumberGenerator();

  protected override ApplicationDbContext CreateDbContext(SqliteConnection connection)
  {
    DbContextOptions<ApplicationDbContext> options =
      new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite(connection)
        .UseSnakeCaseNamingConvention()
        .Options;

    return new ApplicationDbContext(options, _dispatcher, Clock);
  }

  private CreateWebReservationCommandHandler CreateSut() => new(Db, Clock, _numberGenerator);

  private static IReservationNumberGenerator StubNumberGenerator()
  {
    int counter = 0;
    IReservationNumberGenerator stub = Substitute.For<IReservationNumberGenerator>();
    stub.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(_ => Task.FromResult($"R-TEST/{Interlocked.Increment(ref counter):D4}"));
    return stub;
  }

  private static CreateWebReservationCommand Command(
      IReadOnlyList<RequestedSpotGroup> requested,
      Guid? groupReservationId = null,
      string? secret = null,
      DateOnly? from = null,
      DateOnly? to = null)
      => new(
          Name: "Web",
          Surname: "Guest",
          Email: "web@example.com",
          Phone: "+420111222333",
          From: from ?? QFrom,
          To: to ?? QTo,
          RequestedSpots: requested,
          Note: null,
          GroupReservationId: groupReservationId,
          GroupReservationSecret: secret);

  private async Task<DomainSpotGroup> SeedGroup(uint capacity, bool isActive = true)
  {
    DomainSpotGroup group = new SpotGroupBuilder()
      .WithCapacity(capacity)
      .Build();
    if (!isActive)
    {
      group.IsActive = false;
    }
    Db.SpotGroups.Add(group);
    await Db.SaveChangesAsync();
    return group;
  }

  private async Task<DomainSpot> SeedSpot(Guid spotGroupId)
  {
    DomainSpot spot = new SpotBuilder().InGroup(spotGroupId).Build();
    Db.Spots.Add(spot);
    await Db.SaveChangesAsync();
    return spot;
  }

  private async Task SeedSpots(Guid spotGroupId, int count)
  {
    for (int i = 0; i < count; i++)
    {
      await SeedSpot(spotGroupId);
    }
  }

  private async Task SeedConfirmedReservationWithItem(
      Guid spotGroupId,
      Guid spotId,
      DateOnly from,
      DateOnly to,
      ReservationState state = ReservationState.Confirmed)
  {
    DomainReservation reservation = new ReservationBuilder()
      .InState(state)
      .For(from, to)
      .Build();
    Db.Reservations.Add(reservation);
    Db.ReservationSpotItems.Add(new DomainReservationSpotItem
    {
      Id = Guid.NewGuid(),
      ReservationId = reservation.Id,
      SpotGroupId = spotGroupId,
      SpotId = spotId,
    });
    await Db.SaveChangesAsync();
  }

  private async Task SeedSpotOutOfOrder(Guid spotId, DateOnly from, DateOnly to)
  {
    OutOfOrder oof = new OutOfOrderBuilder().Between(from, to).Build();
    Db.OutOfOrders.Add(oof);
    Db.SpotOofItems.Add(new SpotOofItem { Id = Guid.NewGuid(), SpotId = spotId, OutOfOrderId = oof.Id });
    await Db.SaveChangesAsync();
  }

  private async Task SeedGroupOutOfOrder(Guid spotGroupId, DateOnly from, DateOnly to)
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
      string secret = "abc123",
      IReadOnlyList<Guid>? heldSpotIds = null)
  {
    GroupReservation group = new GroupReservationBuilder()
      .For(from, to)
      .InState(state)
      .WithSecret(secret)
      .HoldingSpots([.. heldSpotIds ?? []])
      .Build();
    Db.GroupReservations.Add(group);
    await Db.SaveChangesAsync();
    return group;
  }

  [Fact]
  public async Task Handle_NoGroupId_AllSpotsAvailable_CreatesReservationInCreatedState()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 5);
    await SeedSpots(grp.Id, 5);

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 2)]),
        CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation r = await Db.Reservations.SingleAsync();
    r.State.ShouldBe(ReservationState.Created);
    r.GroupReservationId.ShouldBeNull();
    List<DomainReservationSpotItem> items = await Db.ReservationSpotItems
        .Where(i => i.ReservationId == r.Id).ToListAsync();
    items.Count.ShouldBe(2);
    items.ShouldAllBe(i => i.SpotId == null && i.SpotGroupId == grp.Id);
  }

  [Fact]
  public async Task Handle_HappyPath_PersistsLowercaseHexSecretOnReservation()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 5);
    await SeedSpots(grp.Id, 1);

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 1)]),
        CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation persisted = await Db.Reservations.SingleAsync(r => r.Id == result.Value.Id);
    persisted.Secret.Length.ShouldBe(64);
    persisted.Secret.ShouldMatch("^[0-9a-f]{64}$");
  }

  [Fact]
  public async Task Handle_HappyPath_ReturnsReservationNumber()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 5);
    await SeedSpots(grp.Id, 1);

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 1)]),
        CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation persisted = await Db.Reservations.SingleAsync(r => r.Id == result.Value.Id);
    result.Value.Number.ShouldBe(persisted.Number);
    result.Value.Number.ShouldNotBeNullOrWhiteSpace();
  }

  [Fact]
  public async Task Handle_CalledTwice_EachReservationGetsItsOwnSecret()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 5);
    await SeedSpots(grp.Id, 2);

    Result<CreateWebReservationResponse> first = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 1)]),
        CancellationToken.None);
    Result<CreateWebReservationResponse> second = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 1)]),
        CancellationToken.None);

    DomainReservation firstPersisted = await Db.Reservations.SingleAsync(r => r.Id == first.Value.Id);
    DomainReservation secondPersisted = await Db.Reservations.SingleAsync(r => r.Id == second.Value.Id);
    firstPersisted.Secret.ShouldNotBe(secondPersisted.Secret);
  }

  [Fact]
  public async Task Handle_GroupIdProvided_GroupNotFound_ReturnsGroupNotFound()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 5);
    var missing = Guid.NewGuid();

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 1)], groupReservationId: missing, secret: "any"),
        CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(GroupReservationErrors.NotFound(missing));
  }

  [Fact]
  public async Task Handle_GroupIdProvided_GroupCanceled_ReturnsCanceled()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 5);
    GroupReservation gr = await SeedGroupReservation(
        new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 20),
        state: GroupReservationState.Canceled,
        secret: "s");

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 1)], groupReservationId: gr.Id, secret: "s"),
        CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(GroupReservationErrors.Canceled(gr.Id));
  }

  [Fact]
  public async Task Handle_GroupIdProvided_SecretMismatch_ReturnsSecretInvalid()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 5);
    GroupReservation gr = await SeedGroupReservation(
        new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 20),
        secret: "real-secret");

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 1)], groupReservationId: gr.Id, secret: "wrong"),
        CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(GroupReservationErrors.SecretInvalid);
  }

  [Fact]
  public async Task Handle_GroupIdProvided_RequestedPeriodBeforeGroup_ReturnsPeriodOutsideGroup()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 5);
    GroupReservation gr = await SeedGroupReservation(
        new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 20),
        secret: "s");

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 1)],
            groupReservationId: gr.Id, secret: "s",
            from: new DateOnly(2026, 7, 1), to: new DateOnly(2026, 7, 10)),
        CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(GroupReservationErrors.PeriodOutsideGroup(gr.Id));
  }

  [Fact]
  public async Task Handle_GroupIdProvided_RequestedPeriodAfterGroup_ReturnsPeriodOutsideGroup()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 5);
    GroupReservation gr = await SeedGroupReservation(
        new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 20),
        secret: "s");

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 1)],
            groupReservationId: gr.Id, secret: "s",
            from: new DateOnly(2026, 8, 1), to: new DateOnly(2026, 8, 10)),
        CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(GroupReservationErrors.PeriodOutsideGroup(gr.Id));
  }

  [Fact]
  public async Task Handle_GroupIdProvided_PeriodsTouchOnBoundaryDay_IsAcceptedAsOverlap()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 5);
    await SeedSpots(grp.Id, 1);
    // Group ends on the same day command starts - handler uses inclusive comparison.
    GroupReservation gr = await SeedGroupReservation(
        new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 10),
        secret: "s");

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 1)],
            groupReservationId: gr.Id, secret: "s",
            from: new DateOnly(2026, 7, 10), to: new DateOnly(2026, 7, 12)),
        CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation persisted = await Db.Reservations.SingleAsync();
    persisted.GroupReservationId.ShouldBe(gr.Id);
  }

  [Fact]
  public async Task Handle_SpotGroupNotFound_ReturnsSpotGroupNotFound()
  {
    var missing = Guid.NewGuid();

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(missing, 1)]),
        CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.SpotGroupNotFound(missing));
  }

  [Fact]
  public async Task Handle_SpotGroupInactive_ReturnsSpotGroupInactive()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 5, isActive: false);

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 1)]),
        CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.SpotGroupInactive(grp.Id));
  }

  [Fact]
  public async Task Handle_QuantityExceedsRawCapacity_ReturnsRequestedQuantityExceedsCapacity()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 3);

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 4)]),
        CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.RequestedQuantityExceedsCapacity(grp.Id));
  }

  [Fact]
  public async Task Handle_QuantityEqualsAvailable_Succeeds()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 5);
    DomainSpot sA = await SeedSpot(grp.Id);
    DomainSpot sB = await SeedSpot(grp.Id);
    await SeedSpots(grp.Id, 3);
    await SeedConfirmedReservationWithItem(grp.Id, sA.Id, QFrom, QTo);
    await SeedConfirmedReservationWithItem(grp.Id, sB.Id, QFrom, QTo);

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 3)]),
        CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_ReservedPlusSpotOooPlusGroupHeld_ReducesAvailability()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 10);
    // 3 reserved
    for (int i = 0; i < 3; i++)
    {
      DomainSpot s = await SeedSpot(grp.Id);
      await SeedConfirmedReservationWithItem(grp.Id, s.Id, QFrom, QTo);
    }
    // 2 spot-level OOO
    for (int i = 0; i < 2; i++)
    {
      DomainSpot s = await SeedSpot(grp.Id);
      await SeedSpotOutOfOrder(s.Id,
          new DateOnly(2026, 7, 11),
          new DateOnly(2026, 7, 14));
    }
    // 4 held by group reservation
    var heldIds = new Guid[4];
    for (int i = 0; i < 4; i++)
    {
      DomainSpot s = await SeedSpot(grp.Id);
      heldIds[i] = s.Id;
    }
    await SeedGroupReservation(QFrom, QTo, heldSpotIds: heldIds);
    // Top up to 10 spots so totalSpots matches capacity.
    await SeedSpots(grp.Id, 1);

    // 10 - 3 - 2 - 4 = 1 available
    Result<CreateWebReservationResponse> okFor1 = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 1)]),
        CancellationToken.None);
    okFor1.IsSuccess.ShouldBeTrue();

    Result<CreateWebReservationResponse> failFor2 = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 2)]),
        CancellationToken.None);
    failFor2.IsFailure.ShouldBeTrue();
    failFor2.Error.ShouldBe(ReservationErrors.RequestedQuantityExceedsCapacity(grp.Id));
  }

  [Fact]
  public async Task Handle_GroupLevelOoo_TreatsGroupAsFullyOccupied()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 5);
    await SeedGroupOutOfOrder(grp.Id,
        new DateOnly(2026, 7, 8),
        new DateOnly(2026, 7, 20));

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 1)]),
        CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.RequestedQuantityExceedsCapacity(grp.Id));
  }

  [Fact]
  public async Task Handle_AllowedGroupReservation_ExcludesItsOwnHeldSpotsFromCompetition()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 5);
    // 4 of 5 spots are held by a group reservation with valid secret
    var heldIds = new Guid[4];
    for (int i = 0; i < 4; i++)
    {
      DomainSpot s = await SeedSpot(grp.Id);
      heldIds[i] = s.Id;
    }
    GroupReservation gr = await SeedGroupReservation(
        QFrom, QTo, secret: "s", heldSpotIds: heldIds);

    // Without the secret, only 1 of 5 is available → qty 4 must fail.
    Result<CreateWebReservationResponse> noSecret = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 4)]),
        CancellationToken.None);
    noSecret.IsFailure.ShouldBeTrue();

    // With the secret, held spots are excluded → qty 4 succeeds.
    Result<CreateWebReservationResponse> withSecret = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 4)],
            groupReservationId: gr.Id, secret: "s"),
        CancellationToken.None);
    withSecret.IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_ReservationsInCreatedOrCancelledState_DoNotReduceAvailability()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 2);
    DomainSpot s1 = await SeedSpot(grp.Id);
    DomainSpot s2 = await SeedSpot(grp.Id);
    await SeedConfirmedReservationWithItem(grp.Id, s1.Id, QFrom, QTo, state: ReservationState.Created);
    await SeedConfirmedReservationWithItem(grp.Id, s2.Id, QFrom, QTo, state: ReservationState.Cancelled);

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 2)]),
        CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_ExistingReservationEndsOnQueryFrom_IsCountedAsOverlap()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 1);
    DomainSpot s = await SeedSpot(grp.Id);
    // Existing reservation ends on QFrom (inclusive overlap).
    await SeedConfirmedReservationWithItem(grp.Id, s.Id,
        from: new DateOnly(2026, 7, 5), to: QFrom);

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 1)]),
        CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.RequestedQuantityExceedsCapacity(grp.Id));
  }

  [Fact]
  public async Task Handle_ZeroCapacityGroup_RejectsAnyQuantity()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 0);

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 1)]),
        CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.RequestedQuantityExceedsCapacity(grp.Id));
  }

  [Fact]
  public async Task Handle_CanceledGroupReservation_DoesNotReduceAvailability()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 2);
    DomainSpot s1 = await SeedSpot(grp.Id);
    DomainSpot s2 = await SeedSpot(grp.Id);
    await SeedGroupReservation(QFrom, QTo,
        state: GroupReservationState.Canceled, heldSpotIds: [s1.Id, s2.Id]);

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 2)]),
        CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_UsesDateTimeProviderForCreatedAtUtc()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 5);
    await SeedSpots(grp.Id, 1);
    var fixedInstant = new DateTime(2026, 6, 15, 13, 45, 0, DateTimeKind.Utc);
    Clock.Set(fixedInstant);

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 1)]),
        CancellationToken.None);

    DomainReservation persisted = await Db.Reservations.SingleAsync(r => r.Id == result.Value.Id);
    persisted.CreatedAtUtc.ShouldBe(fixedInstant);
  }

  [Fact]
  public async Task Handle_SpotGroupOooButDifferentPeriod_DoesNotOccupy()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 2);
    await SeedSpots(grp.Id, 2);
    // Group-level OOO that does NOT overlap the query window.
    await SeedGroupOutOfOrder(grp.Id,
        new DateOnly(2026, 6, 1),
        new DateOnly(2026, 6, 5));

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(
        Command([new RequestedSpotGroup(grp.Id, 2)]),
        CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_OnSuccess_DoesNotRaiseReservationConfirmedDomainEvent()
  {
    DomainSpotGroup grp = await SeedGroup(capacity: 5);
    await SeedSpots(grp.Id, 1);

    CreateWebReservationCommand command = Command([new RequestedSpotGroup(grp.Id, 1)]) with { Language = ReservationLanguages.English };

    Result<CreateWebReservationResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation reservation = await Db.Reservations.SingleAsync(r => r.Id == result.Value.Id);
    reservation.Language.ShouldBe(ReservationLanguages.English);
    _dispatcher.Dispatched.OfType<ReservationConfirmedDomainEvent>().ShouldBeEmpty();
  }
}
