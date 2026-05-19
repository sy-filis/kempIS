using Application.Abstractions.Reservations;
using Application.Reservations.Commands.CreateGroupReservation;
using Domain.Common;
using Domain.Reservations;
using Domain.Reservations.GroupReservations;
using Domain.Reservations.GroupReservations.DomainEvents;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.Data.Sqlite;
using SharedKernel;

namespace Application.UnitTests.Reservations.Commands.CreateGroupReservation;

public sealed class CreateGroupReservationCommandHandlerTests : HandlerTestBase
{
  private readonly ISpotAvailabilityChecker _availability = Substitute.For<ISpotAvailabilityChecker>();
  private readonly IGroupReservationNumberGenerator _numberGenerator = StubNumberGenerator();
  private readonly CapturingDomainEventsDispatcher _dispatcher = new();

  private static IGroupReservationNumberGenerator StubNumberGenerator()
  {
    int counter = 0;
    IGroupReservationNumberGenerator stub = Substitute.For<IGroupReservationNumberGenerator>();
    stub.NextAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
      .Returns(_ => Task.FromResult($"GR-TEST/{Interlocked.Increment(ref counter):D4}"));
    return stub;
  }

  protected override ApplicationDbContext CreateDbContext(SqliteConnection connection)
  {
    DbContextOptions<ApplicationDbContext> options =
      new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite(connection)
        .UseSnakeCaseNamingConvention()
        .Options;

    return new ApplicationDbContext(options, _dispatcher, Clock);
  }

  private CreateGroupReservationCommandHandler CreateSut() => new(Db, _availability, Clock, _numberGenerator);

  private static CreateGroupReservationCommand Command(
      IReadOnlyList<Guid> spotIds,
      DateOnly? from = null,
      DateOnly? to = null,
      string organizerPhone = "+420 777 123 456",
      string language = ReservationLanguages.Czech)
      => new(
          From: from ?? new DateOnly(2026, 7, 1),
          To: to ?? new DateOnly(2026, 7, 10),
          SpotIds: spotIds,
          OrganizerName: "Organizer",
          OrganizerEmail: "org@example.com",
          OrganizerPhone: organizerPhone,
          Note: null,
          Language: language);

  private async Task<Guid> SeedSpot()
  {
    Domain.Reservations.Spots.Spot spot = new SpotBuilder().Build();
    Db.Spots.Add(spot);
    await Db.SaveChangesAsync();
    return spot.Id;
  }

  [Fact]
  public async Task Handle_AllSpotsExistAndAvailable_CreatesGroupAndReturnsSecret()
  {
    Guid s1 = await SeedSpot();
    Guid s2 = await SeedSpot();
    Guid s3 = await SeedSpot();
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());

    Result<CreateGroupReservationResponse> result = await CreateSut().Handle(
        Command([s1, s2, s3]), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Secret.Length.ShouldBe(64); // 32 bytes as lowercase hex
    result.Value.Secret.ShouldMatch("^[0-9a-f]{64}$");
    GroupReservation persisted = await Db.GroupReservations
        .Include(g => g.HeldSpots)
        .SingleAsync(g => g.Id == result.Value.Id);
    persisted.State.ShouldBe(GroupReservationState.Confirmed);
    persisted.HeldSpots.Count.ShouldBe(3);
    persisted.HeldSpots.Select(h => h.SpotId).ShouldBe(new[] { s1, s2, s3 }, ignoreOrder: true);
    persisted.CreatedAtUtc.ShouldBe(Clock.UtcNow);
    persisted.OrganizerEmail.ShouldBe("org@example.com");
    persisted.OrganizerPhone.ShouldBe("+420 777 123 456");
  }

  [Fact]
  public async Task Handle_CalledTwice_GeneratesDifferentSecrets()
  {
    Guid spotId = await SeedSpot();
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());

    Result<CreateGroupReservationResponse> first = await CreateSut().Handle(
        Command([spotId]), CancellationToken.None);
    Result<CreateGroupReservationResponse> second = await CreateSut().Handle(
        Command([spotId]), CancellationToken.None);

    first.Value.Secret.ShouldNotBe(second.Value.Secret);
  }

  [Fact]
  public async Task Handle_DuplicateSpotIdsInCommand_DeDupedBeforeChecking()
  {
    Guid spotId = await SeedSpot();
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());

    Result<CreateGroupReservationResponse> result = await CreateSut().Handle(
        Command([spotId, spotId, spotId]), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    GroupReservation persisted = await Db.GroupReservations
        .Include(g => g.HeldSpots)
        .SingleAsync();
    persisted.HeldSpots.Count.ShouldBe(1);
    await _availability.Received(1).CheckAsync(
        Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(spotId)),
        Arg.Any<DateRange>(),
        Arg.Any<SpotAvailabilityContext>(),
        Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_MissingSpot_ReturnsSpotNotFound_AndSkipsAvailabilityCheck()
  {
    Guid existing = await SeedSpot();
    var missing = Guid.NewGuid();

    Result<CreateGroupReservationResponse> result = await CreateSut().Handle(
        Command([existing, missing]), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.SpotNotFound(missing));
    await _availability.DidNotReceiveWithAnyArgs().CheckAsync(default!, default!, default!, default);
    (await Db.GroupReservations.AnyAsync()).ShouldBeFalse();
  }

  [Fact]
  public async Task Handle_AvailabilityFails_PropagatesErrorAndPersistsNothing()
  {
    Guid spotId = await SeedSpot();
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Failure(ReservationErrors.SpotOccupiedByOutOfOrder(spotId)));

    Result<CreateGroupReservationResponse> result = await CreateSut().Handle(
        Command([spotId]), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.SpotOccupiedByOutOfOrder(spotId));
    (await Db.GroupReservations.AnyAsync()).ShouldBeFalse();
  }

  [Fact]
  public async Task Handle_HappyPath_DispatchesGroupReservationCreatedDomainEvent()
  {
    Guid spotId = await SeedSpot();
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());

    Result<CreateGroupReservationResponse> result = await CreateSut().Handle(
        Command([spotId]), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    _dispatcher.Dispatched.OfType<GroupReservationCreatedDomainEvent>()
        .ShouldHaveSingleItem()
        .GroupReservationId.ShouldBe(result.Value.Id);
  }

  [Fact]
  public async Task Handle_UsesDateTimeProviderForCreatedAtUtc()
  {
    Guid spotId = await SeedSpot();
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());
    DateTime fixedInstant = new(2026, 6, 1, 7, 30, 0, DateTimeKind.Utc);
    Clock.Set(fixedInstant);

    Result<CreateGroupReservationResponse> result = await CreateSut().Handle(
        Command([spotId]), CancellationToken.None);

    GroupReservation persisted = await Db.GroupReservations.SingleAsync(g => g.Id == result.Value.Id);
    persisted.CreatedAtUtc.ShouldBe(fixedInstant);
  }

  [Fact]
  public async Task Handle_AvailabilityCheckerCalled_WithoutAllowGroupOverlap()
  {
    Guid spotId = await SeedSpot();
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());

    await CreateSut().Handle(Command([spotId]), CancellationToken.None);

    await _availability.Received(1).CheckAsync(
        Arg.Any<IReadOnlyCollection<Guid>>(),
        Arg.Any<DateRange>(),
        Arg.Is<SpotAvailabilityContext>(ctx =>
            ctx.AllowGroupOverlap == null
            && ctx.ExcludeReservationId == null
            && ctx.ExcludeGroupReservationId == null),
        Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_HappyPath_GeneratesAndReturnsGroupReservationNumber()
  {
    Guid spotId = await SeedSpot();
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());

    Result<CreateGroupReservationResponse> result = await CreateSut().Handle(
        Command([spotId]), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Number.ShouldNotBeNullOrWhiteSpace();
    GroupReservation persisted = await Db.GroupReservations.AsNoTracking()
        .SingleAsync(g => g.Id == result.Value.Id);
    persisted.Number.ShouldBe(result.Value.Number);
  }

  [Fact]
  public async Task Handle_WithDisplayName_PersistsDisplayName()
  {
    Guid spotId = await SeedSpot();
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());

    CreateGroupReservationCommand command = Command([spotId]) with { DisplayName = "Company retreat" };

    Result<CreateGroupReservationResponse> result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    GroupReservation persisted = await Db.GroupReservations.AsNoTracking().SingleAsync();
    persisted.DisplayName.ShouldBe("Company retreat");
  }

  [Fact]
  public async Task Handle_PersistsSuppliedLanguage()
  {
    Guid spotId = await SeedSpot();
    _availability.CheckAsync(default!, default!, default!, default)
        .ReturnsForAnyArgs(Result.Success());

    Result<CreateGroupReservationResponse> result = await CreateSut().Handle(
        Command([spotId], language: ReservationLanguages.English), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    GroupReservation persisted = await Db.GroupReservations.AsNoTracking().SingleAsync();
    persisted.Language.ShouldBe(ReservationLanguages.English);
  }
}
