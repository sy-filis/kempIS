using Application.Abstractions.Reservations;
using Application.Reservations.Commands.CreateReservation;
using Domain.Common;
using Domain.Reservations;
using Domain.Reservations.ReservationStates;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.Data.Sqlite;
using SharedKernel;
using DomainReservation = Domain.Reservations.Reservations.Reservation;
using DomainReservationSpotItem = Domain.Reservations.ReservationSpotItems.ReservationSpotItem;

namespace Application.UnitTests.Reservations.Commands.CreateReservation;

public sealed class CreateReservationCommandHandlerTests : HandlerTestBase
{
  private readonly ISpotAvailabilityChecker _availability = Substitute.For<ISpotAvailabilityChecker>();
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

  private CreateReservationCommandHandler CreateSut() => new(Db, _availability, Clock, _numberGenerator);

  private static IReservationNumberGenerator StubNumberGenerator()
  {
    int counter = 0;
    IReservationNumberGenerator stub = Substitute.For<IReservationNumberGenerator>();
    stub.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(_ => Task.FromResult($"R-TEST/{Interlocked.Increment(ref counter):D4}"));
    return stub;
  }

  private static CreateReservationCommand Command(
      IReadOnlyList<Guid> spotIds,
      Guid? groupReservationId = null,
      DateOnly? from = null,
      DateOnly? to = null,
      IReadOnlyList<ReservationServiceLine>? services = null,
      IReadOnlyList<ReservationVehicleLine>? vehicles = null)
      => new(
          Name: "Jan",
          Surname: "Novak",
          Email: "jan@example.com",
          Phone: "+420000000000",
          From: from ?? new DateOnly(2026, 6, 1),
          To: to ?? new DateOnly(2026, 6, 3),
          SpotIds: spotIds,
          Note: null,
          GroupReservationId: groupReservationId,
          Services: services ?? Array.Empty<ReservationServiceLine>(),
          Vehicles: vehicles ?? Array.Empty<ReservationVehicleLine>());

  private async Task<Guid> SeedGroup(uint capacity = 5)
  {
    Domain.Reservations.SpotGroups.SpotGroup group = new SpotGroupBuilder().WithCapacity(capacity).Build();
    Db.SpotGroups.Add(group);
    await Db.SaveChangesAsync();
    return group.Id;
  }

  private async Task<Guid> SeedSpot(Guid groupId, string name = "Spot")
  {
    Domain.Reservations.Spots.Spot spot = new SpotBuilder().InGroup(groupId).WithName(name).Build();
    Db.Spots.Add(spot);
    await Db.SaveChangesAsync();
    return spot.Id;
  }

  [Fact]
  public async Task Handle_AllSpotsExistAndAvailable_CreatesConfirmedReservationWithSpotItems()
  {
    Guid groupId = await SeedGroup();
    Guid spotA = await SeedSpot(groupId, "A");
    Guid spotB = await SeedSpot(groupId, "B");
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());

    Result<Guid> result = await CreateSut().Handle(Command([spotA, spotB]), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation persisted = await Db.Reservations.SingleAsync();
    persisted.Id.ShouldBe(result.Value);
    persisted.State.ShouldBe(ReservationState.Confirmed);
    persisted.Period.ShouldBe(new DateRange(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3)));
    persisted.CreatedAtUtc.ShouldBe(Clock.UtcNow);
    persisted.ReservationMaker.Email.ShouldBe("jan@example.com");

    List<DomainReservationSpotItem> items = await Db.ReservationSpotItems
        .Where(i => i.ReservationId == persisted.Id)
        .ToListAsync();
    items.Count.ShouldBe(2);
    items.ShouldAllBe(i => i.SpotGroupId == groupId);
    items.Select(i => i.SpotId).ShouldBe(new Guid?[] { spotA, spotB }, ignoreOrder: true);
  }

  [Fact]
  public async Task Handle_OneRequestedSpotMissing_ReturnsSpotNotFound_AndSkipsAvailabilityCheck()
  {
    Guid groupId = await SeedGroup();
    Guid existing = await SeedSpot(groupId);
    var missing = Guid.NewGuid();

    Result<Guid> result = await CreateSut().Handle(Command([existing, missing]), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.SpotNotFound(missing));
    await _availability.DidNotReceiveWithAnyArgs().CheckAsync(default!, default!, default!, default);
    (await Db.Reservations.AnyAsync()).ShouldBeFalse();
  }

  [Fact]
  public async Task Handle_AvailabilityCheckFails_PropagatesErrorAndSkipsPersist()
  {
    Guid groupId = await SeedGroup();
    Guid spotId = await SeedSpot(groupId);
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Failure(ReservationErrors.SpotOccupiedByReservation(spotId)));

    Result<Guid> result = await CreateSut().Handle(Command([spotId]), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.SpotOccupiedByReservation(spotId));
    (await Db.Reservations.AnyAsync()).ShouldBeFalse();
    (await Db.ReservationSpotItems.AnyAsync()).ShouldBeFalse();
  }

  [Fact]
  public async Task Handle_GroupReservationIdProvided_AvailabilityCheckerReceivesAllowGroupOverlap()
  {
    Guid groupId = await SeedGroup();
    Guid spotId = await SeedSpot(groupId);
    var groupReservationId = Guid.NewGuid();
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());

    await CreateSut().Handle(
        Command([spotId], groupReservationId: groupReservationId),
        CancellationToken.None);

    await _availability.Received(1).CheckAsync(
        Arg.Any<IReadOnlyCollection<Guid>>(),
        Arg.Any<DateRange>(),
        Arg.Is<SpotAvailabilityContext>(ctx => ctx.AllowGroupOverlap == groupReservationId),
        Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_HappyPath_DispatchesReservationCreatedDomainEvent()
  {
    Guid groupId = await SeedGroup();
    Guid spotId = await SeedSpot(groupId);
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());

    Result<Guid> result = await CreateSut().Handle(Command([spotId]), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    _dispatcher.Dispatched.OfType<ReservationCreatedDomainEvent>()
        .ShouldHaveSingleItem()
        .ReservationId.ShouldBe(result.Value);
  }

  [Fact]
  public async Task Handle_UsesDateTimeProviderForCreatedAtUtc()
  {
    Guid groupId = await SeedGroup();
    Guid spotId = await SeedSpot(groupId);
    DateTime fixedInstant = new(2026, 4, 20, 9, 30, 0, DateTimeKind.Utc);
    Clock.Set(fixedInstant);
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());

    Result<Guid> result = await CreateSut().Handle(Command([spotId]), CancellationToken.None);

    DomainReservation persisted = await Db.Reservations.SingleAsync(r => r.Id == result.Value);
    persisted.CreatedAtUtc.ShouldBe(fixedInstant);
  }

  [Fact]
  public async Task Handle_SpotsInDifferentGroups_EachSpotItemCarriesItsOwnGroupId()
  {
    Guid groupA = await SeedGroup();
    Guid groupB = await SeedGroup();
    Guid spotA = await SeedSpot(groupA, "A");
    Guid spotB = await SeedSpot(groupB, "B");
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());

    Result<Guid> result = await CreateSut().Handle(Command([spotA, spotB]), CancellationToken.None);

    List<DomainReservationSpotItem> items = await Db.ReservationSpotItems
        .Where(i => i.ReservationId == result.Value)
        .ToListAsync();
    items.Single(i => i.SpotId == spotA).SpotGroupId.ShouldBe(groupA);
    items.Single(i => i.SpotId == spotB).SpotGroupId.ShouldBe(groupB);
  }

  [Fact]
  public async Task Handle_CancelledToken_ThrowsAndDoesNotPersist()
  {
    Guid groupId = await SeedGroup();
    Guid spotId = await SeedSpot(groupId);
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());
    using var cts = new CancellationTokenSource();
    await cts.CancelAsync();

    await Should.ThrowAsync<OperationCanceledException>(async () =>
        await CreateSut().Handle(Command([spotId]), cts.Token));

    (await Db.Reservations.AnyAsync()).ShouldBeFalse();
  }

  [Fact]
  public async Task Handle_WithServices_PersistsServiceItemsWithServiceId()
  {
    Guid groupId = await SeedGroup();
    Guid spotId = await SeedSpot(groupId);
    Guid serviceId = await ServiceBuilder.SeedAsync(Db);
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    CreateReservationCommand command = Command(
      spotIds: [spotId],
      services: [new ReservationServiceLine(serviceId, Quantity: 4u, RecapSingleQuantity: 1u, RecapDayQuantity: 2u)]);

    Result<Guid> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Domain.Reservations.ReservationServiceItems.ReservationServiceItem rsi =
      await Db.ReservationServiceItems.AsNoTracking().SingleAsync();
    rsi.ServiceId.ShouldBe(serviceId);
    rsi.Quantity.ShouldBe(4u);
    rsi.RecapSingleQuantity.ShouldBe(1u);
    rsi.RecapDayQuantity.ShouldBe(2u);
  }

  [Fact]
  public async Task Handle_WithVehicles_PersistsVehiclesWithoutBillOrService()
  {
    Guid groupId = await SeedGroup();
    Guid spotId = await SeedSpot(groupId);
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    CreateReservationCommand command = Command(
      spotIds: [spotId],
      vehicles: [new ReservationVehicleLine(Id: null, RegistrationNumber: "1AB-2345")]);

    Result<Guid> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Domain.Reservations.Vehicles.Vehicle v = await Db.Vehicles.AsNoTracking().SingleAsync();
    v.RegistrationNumber.ShouldBe("1AB-2345");
    v.BillId.ShouldBeNull();
    v.ServiceId.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_UnknownServiceId_ReturnsServiceNotFound()
  {
    Guid groupId = await SeedGroup();
    Guid spotId = await SeedSpot(groupId);
    var missingService = Guid.NewGuid();
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    CreateReservationCommand command = Command(
      spotIds: [spotId],
      services: [new ReservationServiceLine(missingService, 1u, 0u, 0u)]);

    Result<Guid> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.ServiceNotFound(missingService));
  }

  [Fact]
  public async Task Handle_WithDisplayName_PersistsDisplayName()
  {
    Guid groupId = await SeedGroup();
    Guid spotId = await SeedSpot(groupId);
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());

    CreateReservationCommand command = Command([spotId]) with { DisplayName = "Smith family" };

    Result<Guid> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation persisted = await Db.Reservations.AsNoTracking().SingleAsync();
    persisted.DisplayName.ShouldBe("Smith family");
  }

  [Fact]
  public async Task Handle_WithoutDisplayName_PersistsNull()
  {
    Guid groupId = await SeedGroup();
    Guid spotId = await SeedSpot(groupId);
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());

    Result<Guid> result = await CreateSut().Handle(Command([spotId]), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation persisted = await Db.Reservations.AsNoTracking().SingleAsync();
    persisted.DisplayName.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_OnSuccess_RaisesReservationConfirmedDomainEvent()
  {
    Guid groupId = await SeedGroup();
    Guid spotId = await SeedSpot(groupId);
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());

    CreateReservationCommand command = Command([spotId]) with { Language = ReservationLanguages.Czech };

    Result<Guid> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    _dispatcher.Dispatched.OfType<ReservationConfirmedDomainEvent>()
        .ShouldHaveSingleItem()
        .ReservationId.ShouldBe(result.Value);
  }

  [Fact]
  public async Task Handle_StoresSuppliedLanguage()
  {
    Guid groupId = await SeedGroup();
    Guid spotId = await SeedSpot(groupId);
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());

    CreateReservationCommand command = Command([spotId]) with { Language = ReservationLanguages.English };

    Result<Guid> result = await CreateSut().Handle(command, CancellationToken.None);

    DomainReservation persisted = await Db.Reservations.AsNoTracking().SingleAsync(r => r.Id == result.Value);
    persisted.Language.ShouldBe(ReservationLanguages.English);
  }
}
