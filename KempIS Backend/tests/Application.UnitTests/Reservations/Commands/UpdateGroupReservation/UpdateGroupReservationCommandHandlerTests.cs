using Application.Reservations.Commands.UpdateGroupReservation;
using Domain.Common;
using Domain.Reservations;
using Domain.Reservations.GroupReservations;
using Domain.Reservations.GroupReservations.DomainEvents;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.Data.Sqlite;
using SharedKernel;

namespace Application.UnitTests.Reservations.Commands.UpdateGroupReservation;

public sealed class UpdateGroupReservationCommandHandlerTests : HandlerTestBase
{
  private readonly CapturingDomainEventsDispatcher _dispatcher = new();

  protected override ApplicationDbContext CreateDbContext(SqliteConnection connection)
  {
    DbContextOptions<ApplicationDbContext> options =
      new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite(connection)
        .UseSnakeCaseNamingConvention()
        .Options;

    return new ApplicationDbContext(options, _dispatcher, Clock);
  }

  private UpdateGroupReservationCommandHandler CreateSut() => new(Db, Clock);

  private async Task<Guid> SeedSpot()
  {
    Domain.Reservations.Spots.Spot spot = new SpotBuilder().Build();
    Db.Spots.Add(spot);
    await Db.SaveChangesAsync();
    return spot.Id;
  }

  private async Task<Guid> SeedGroup(
    GroupReservationState state = GroupReservationState.Confirmed,
    DateOnly? from = null,
    DateOnly? to = null,
    IReadOnlyList<Guid>? heldSpotIds = null)
  {
    var id = Guid.NewGuid();
    Db.GroupReservations.Add(new GroupReservation
    {
      Id = id,
      Number = $"GR-TEST/SEED-{Guid.NewGuid():N}",
      State = state,
      Period = new DateRange(from ?? new DateOnly(2026, 7, 1), to ?? new DateOnly(2026, 7, 5)),
      Secret = "seed-secret",
      OrganizerName = "Original Name",
      OrganizerEmail = "original@example.com",
      OrganizerPhone = "+420 700 000 000",
      Note = "Original note",
      CreatedAtUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
      HeldSpots = (heldSpotIds ?? []).Select(spotId => new GroupReservationSpot { SpotId = spotId }).ToList(),
    });
    await Db.SaveChangesAsync();
    return id;
  }

  private static UpdateGroupReservationCommand Cmd(
    Guid groupId,
    IReadOnlyList<Guid>? spotIds = null,
    DateOnly? from = null,
    DateOnly? to = null,
    string organizerName = "Updated Name",
    string organizerEmail = "updated@example.com",
    string organizerPhone = "+420 777 999 888",
    string? note = "Updated note")
    => new(
      Id: groupId,
      From: from ?? new DateOnly(2026, 8, 1),
      To: to ?? new DateOnly(2026, 8, 5),
      SpotIds: spotIds ?? [],
      OrganizerName: organizerName,
      OrganizerEmail: organizerEmail,
      OrganizerPhone: organizerPhone,
      Note: note);

  [Fact]
  public async Task Handle_ValidInput_UpdatesAllFieldsAndHeldSpots()
  {
    Guid oldSpot1 = await SeedSpot();
    Guid oldSpot2 = await SeedSpot();
    Guid newSpot1 = await SeedSpot();
    Guid newSpot2 = await SeedSpot();
    Guid newSpot3 = await SeedSpot();
    Guid groupId = await SeedGroup(heldSpotIds: [oldSpot1, oldSpot2]);
    DateTime fixedInstant = new(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc);
    Clock.Set(fixedInstant);

    Result result = await CreateSut().Handle(
      Cmd(groupId, [newSpot1, newSpot2, newSpot3],
          from: new DateOnly(2026, 8, 10),
          to: new DateOnly(2026, 8, 20)),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    GroupReservation updated = await Db.GroupReservations
      .Include(g => g.HeldSpots)
      .SingleAsync(g => g.Id == groupId);
    updated.Period.From.ShouldBe(new DateOnly(2026, 8, 10));
    updated.Period.To.ShouldBe(new DateOnly(2026, 8, 20));
    updated.OrganizerName.ShouldBe("Updated Name");
    updated.OrganizerEmail.ShouldBe("updated@example.com");
    updated.OrganizerPhone.ShouldBe("+420 777 999 888");
    updated.Note.ShouldBe("Updated note");
    updated.UpdatedAtUtc.ShouldBe(fixedInstant);
    updated.CreatedAtUtc.ShouldBe(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
    updated.HeldSpots.Count.ShouldBe(3);
    updated.HeldSpots.Select(h => h.SpotId).ShouldBe(new[] { newSpot1, newSpot2, newSpot3 }, ignoreOrder: true);
    updated.Secret.ShouldBe("seed-secret");
    updated.State.ShouldBe(GroupReservationState.Confirmed);
  }

  [Fact]
  public async Task Handle_NotFound_ReturnsNotFound()
  {
    Guid spotId = await SeedSpot();
    var unknownId = Guid.NewGuid();

    Result result = await CreateSut().Handle(Cmd(unknownId, [spotId]), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(GroupReservationErrors.NotFound(unknownId));
  }

  [Fact]
  public async Task Handle_GroupCanceled_ReturnsCanceled()
  {
    Guid spotId = await SeedSpot();
    Guid groupId = await SeedGroup(state: GroupReservationState.Canceled, heldSpotIds: [spotId]);

    Result result = await CreateSut().Handle(Cmd(groupId, [spotId]), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(GroupReservationErrors.Canceled(groupId));
  }

  [Fact]
  public async Task Handle_UnknownSpotId_ReturnsSpotNotFound()
  {
    Guid existingSpot = await SeedSpot();
    Guid groupId = await SeedGroup(heldSpotIds: [existingSpot]);
    var missingSpot = Guid.NewGuid();

    Result result = await CreateSut().Handle(
      Cmd(groupId, [existingSpot, missingSpot]), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.SpotNotFound(missingSpot));
  }

  [Fact]
  public async Task Handle_HappyPath_RaisesGroupReservationUpdatedDomainEvent()
  {
    Guid spotId = await SeedSpot();
    Guid groupId = await SeedGroup(heldSpotIds: [spotId]);

    Result result = await CreateSut().Handle(Cmd(groupId, [spotId]), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    _dispatcher.Dispatched.OfType<GroupReservationUpdatedDomainEvent>()
      .ShouldHaveSingleItem()
      .GroupReservationId.ShouldBe(groupId);
  }

  [Fact]
  public async Task Handle_HappyPath_StampsUpdatedAtUtc()
  {
    Guid spotId = await SeedSpot();
    Guid groupId = await SeedGroup(heldSpotIds: [spotId]);
    DateTime fixedInstant = new(2026, 9, 9, 9, 9, 9, DateTimeKind.Utc);
    Clock.Set(fixedInstant);

    await CreateSut().Handle(Cmd(groupId, [spotId]), CancellationToken.None);

    GroupReservation updated = await Db.GroupReservations.SingleAsync(g => g.Id == groupId);
    updated.UpdatedAtUtc.ShouldBe(fixedInstant);
  }

  [Fact]
  public async Task Handle_DuplicateSpotIdsInRequest_DeduplicatesHeldSpots()
  {
    Guid spotId = await SeedSpot();
    Guid groupId = await SeedGroup(heldSpotIds: []);

    Result result = await CreateSut().Handle(
      Cmd(groupId, [spotId, spotId, spotId]), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    GroupReservation updated = await Db.GroupReservations
      .Include(g => g.HeldSpots)
      .SingleAsync(g => g.Id == groupId);
    updated.HeldSpots.Count.ShouldBe(1);
    updated.HeldSpots.Single().SpotId.ShouldBe(spotId);
  }

  [Fact]
  public async Task Handle_DisplayNameSet_PersistsValue()
  {
    Guid spotId = await SeedSpot();
    Guid groupId = await SeedGroup(heldSpotIds: [spotId]);

    Result result = await CreateSut().Handle(
      Cmd(groupId, [spotId]) with { DisplayName = "Company retreat" },
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    GroupReservation reloaded = await Db.GroupReservations.AsNoTracking().SingleAsync(g => g.Id == groupId);
    reloaded.DisplayName.ShouldBe("Company retreat");
  }

  [Fact]
  public async Task Handle_DisplayNameReplaced_OverwritesPrevious()
  {
    Guid spotId = await SeedSpot();
    Guid groupId = await SeedGroup(heldSpotIds: [spotId]);
    GroupReservation group = await Db.GroupReservations.SingleAsync(g => g.Id == groupId);
    group.DisplayName = "old";
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(
      Cmd(groupId, [spotId]) with { DisplayName = "new" },
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    GroupReservation reloaded = await Db.GroupReservations.AsNoTracking().SingleAsync(g => g.Id == groupId);
    reloaded.DisplayName.ShouldBe("new");
  }

  [Fact]
  public async Task Handle_DisplayNameClearedToNull_RemovesPreviousValue()
  {
    Guid spotId = await SeedSpot();
    Guid groupId = await SeedGroup(heldSpotIds: [spotId]);
    GroupReservation group = await Db.GroupReservations.SingleAsync(g => g.Id == groupId);
    group.DisplayName = "old";
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(
      Cmd(groupId, [spotId]) with { DisplayName = null },
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    GroupReservation reloaded = await Db.GroupReservations.AsNoTracking().SingleAsync(g => g.Id == groupId);
    reloaded.DisplayName.ShouldBeNull();
  }
}
