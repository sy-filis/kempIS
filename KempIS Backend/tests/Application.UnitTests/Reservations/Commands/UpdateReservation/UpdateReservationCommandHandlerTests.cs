using Application.Abstractions.Reservations;
using Application.Reservations.Commands.CreateReservation;
using Application.Reservations.Commands.UpdateReservation;
using Domain.Common;
using Domain.Reservations;
using Domain.Reservations.ReservationServiceItems;
using Domain.Reservations.ReservationStates;
using Domain.Reservations.Vehicles;
using Infrastructure.Database;
using Microsoft.Data.Sqlite;
using SharedKernel;
using DomainReservation = Domain.Reservations.Reservations.Reservation;
using DomainReservationSpotItem = Domain.Reservations.ReservationSpotItems.ReservationSpotItem;

namespace Application.UnitTests.Reservations.Commands.UpdateReservation;

public sealed class UpdateReservationCommandHandlerTests : HandlerTestBase
{
  private readonly ISpotAvailabilityChecker _availability = Substitute.For<ISpotAvailabilityChecker>();
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

  private UpdateReservationCommandHandler CreateSut() => new(Db, _availability, Clock);

  private static readonly DateOnly OriginalFrom = new(2026, 6, 1);
  private static readonly DateOnly OriginalTo = new(2026, 6, 5);

  private async Task<DomainReservation> SeedReservation(
      Guid? groupReservationId = null,
      string? note = "original",
      ReservationState state = ReservationState.Confirmed)
  {
    DomainReservation reservation = new ReservationBuilder()
      .InState(state)
      .For(OriginalFrom, OriginalTo)
      .WithNote(note)
      .Build();
    if (groupReservationId is not null)
    {
      reservation.GroupReservationId = groupReservationId;
    }
    Db.Reservations.Add(reservation);
    await Db.SaveChangesAsync();
    return reservation;
  }

  private static UpdateReservationCommand ValidUpdateCommandFor(DomainReservation reservation)
      => BuildCommand(reservation.Id);

  private static UpdateReservationCommand BuildCommand(
      Guid id,
      DateOnly? from = null,
      DateOnly? to = null,
      IReadOnlyList<Guid>? spotIds = null,
      Guid? groupReservationId = null,
      IReadOnlyList<ReservationServiceLine>? services = null,
      IReadOnlyList<ReservationVehicleLine>? vehicles = null,
      string name = "Jan",
      string surname = "Novak",
      string email = "jan@example.com",
      string phone = "+420000000000",
      string? note = "updated")
      => new(
          Id: id,
          Name: name,
          Surname: surname,
          Email: email,
          Phone: phone,
          From: from ?? OriginalFrom,
          To: to ?? OriginalTo,
          Note: note,
          GroupReservationId: groupReservationId,
          SpotIds: spotIds ?? Array.Empty<Guid>(),
          Services: services ?? Array.Empty<ReservationServiceLine>(),
          Vehicles: vehicles ?? Array.Empty<ReservationVehicleLine>());

  [Fact]
  public async Task Handle_CancelledReservation_ReturnsConflict()
  {
    DomainReservation reservation = new ReservationBuilder()
      .InState(ReservationState.Cancelled)
      .For(OriginalFrom, OriginalTo)
      .Build();
    Db.Reservations.Add(reservation);
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(BuildCommand(reservation.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Reservations.NotEditableInState");
  }

  [Fact]
  public async Task Handle_CompletedReservation_ReturnsConflict()
  {
    DomainReservation reservation = new ReservationBuilder()
      .InState(ReservationState.Completed)
      .For(OriginalFrom, OriginalTo)
      .Build();
    Db.Reservations.Add(reservation);
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(BuildCommand(reservation.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Reservations.NotEditableInState");
  }

  [Fact]
  public async Task Handle_UnknownReservation_ReturnsNotFound()
  {
    Result result = await CreateSut().Handle(BuildCommand(Guid.NewGuid()), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Reservation.NotFound");
  }

  [Fact]
  public async Task Handle_MakerEdited_ReplacesMakerFields()
  {
    DomainReservation reservation = await SeedReservation();
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(
      BuildCommand(reservation.Id, name: "Eva", surname: "Bila", email: "eva@example.com", phone: "+420555111222"),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation reloaded = await Db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservation.Id);
    reloaded.ReservationMaker.Name.ShouldBe("Eva");
    reloaded.ReservationMaker.Surname.ShouldBe("Bila");
    reloaded.ReservationMaker.Email.ShouldBe("eva@example.com");
    reloaded.ReservationMaker.Phone.ShouldBe("+420555111222");
  }

  [Fact]
  public async Task Handle_SpotsKept_PreservesHasReturnedKeys()
  {
    DomainReservation reservation = await SeedReservation();
    var spotGroupId = Guid.NewGuid();
    var keptSpotId = Guid.NewGuid();
    Db.SpotGroups.Add(new SpotGroupBuilder().WithId(spotGroupId).WithCapacity(5).Build());
    Db.Spots.Add(new SpotBuilder().WithId(keptSpotId).InGroup(spotGroupId).Build());
    Db.ReservationSpotItems.Add(new DomainReservationSpotItem
    {
      Id = Guid.NewGuid(),
      ReservationId = reservation.Id,
      SpotGroupId = spotGroupId,
      SpotId = keptSpotId,
      HasReturnedKeys = true,
    });
    await Db.SaveChangesAsync();
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(
      BuildCommand(reservation.Id, spotIds: [keptSpotId]),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservationSpotItem reloaded = await Db.ReservationSpotItems.AsNoTracking()
      .SingleAsync(s => s.ReservationId == reservation.Id);
    reloaded.SpotId.ShouldBe(keptSpotId);
    reloaded.HasReturnedKeys.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_ServicesEdited_UpdatesQuantitiesInPlace()
  {
    DomainReservation reservation = await SeedReservation();
    Guid serviceId = await ServiceBuilder.SeedAsync(Db);
    var existingItemId = Guid.NewGuid();
    Db.ReservationServiceItems.Add(new ReservationServiceItem
    {
      Id = existingItemId,
      ReservationId = reservation.Id,
      ServiceId = serviceId,
      Quantity = 1u,
      RecapSingleQuantity = 0u,
      RecapDayQuantity = 0u,
    });
    await Db.SaveChangesAsync();
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(
      BuildCommand(reservation.Id,
        services: [new ReservationServiceLine(serviceId, 5u, 1u, 2u)]),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    ReservationServiceItem reloaded = await Db.ReservationServiceItems.AsNoTracking().SingleAsync();
    reloaded.Id.ShouldBe(existingItemId);
    reloaded.Quantity.ShouldBe(5u);
    reloaded.RecapSingleQuantity.ShouldBe(1u);
    reloaded.RecapDayQuantity.ShouldBe(2u);
  }

  [Fact]
  public async Task Handle_ServiceRemoved_DeletesRow()
  {
    DomainReservation reservation = await SeedReservation();
    Guid serviceId = await ServiceBuilder.SeedAsync(Db);
    Db.ReservationServiceItems.Add(new ReservationServiceItem
    {
      Id = Guid.NewGuid(),
      ReservationId = reservation.Id,
      ServiceId = serviceId,
      Quantity = 1u,
      RecapSingleQuantity = 0u,
      RecapDayQuantity = 0u,
    });
    await Db.SaveChangesAsync();
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(BuildCommand(reservation.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    (await Db.ReservationServiceItems.AsNoTracking().CountAsync()).ShouldBe(0);
  }

  [Fact]
  public async Task Handle_VehicleKept_PreservesBillAndServiceLink()
  {
    DomainReservation reservation = await SeedReservation();
    var existingVehicleId = Guid.NewGuid();
    var billId = Guid.NewGuid();
    var linkedServiceId = Guid.NewGuid();
    Db.Vehicles.Add(new Vehicle
    {
      Id = existingVehicleId,
      ReservationId = reservation.Id,
      BillId = billId,
      ServiceId = linkedServiceId,
      RegistrationNumber = "OLD",
    });
    await Db.SaveChangesAsync();
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(
      BuildCommand(reservation.Id,
        vehicles: [new ReservationVehicleLine(Id: existingVehicleId, RegistrationNumber: "NEW")]),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Vehicle reloaded = await Db.Vehicles.AsNoTracking().SingleAsync();
    reloaded.Id.ShouldBe(existingVehicleId);
    reloaded.RegistrationNumber.ShouldBe("NEW");
    reloaded.BillId.ShouldBe(billId);
    reloaded.ServiceId.ShouldBe(linkedServiceId);
  }

  [Fact]
  public async Task Handle_VehicleIdFromAnotherReservation_ReturnsVehicleNotOnReservation()
  {
    DomainReservation reservation = await SeedReservation();
    var foreignVehicleId = Guid.NewGuid();
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(
      BuildCommand(reservation.Id,
        vehicles: [new ReservationVehicleLine(Id: foreignVehicleId, RegistrationNumber: "X")]),
      CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Reservations.VehicleNotOnReservation");
  }

  [Fact]
  public async Task Handle_GroupLinkChanged_PassesNewGroupIdToAvailabilityCheck()
  {
    DomainReservation reservation = await SeedReservation();
    var newGroupId = Guid.NewGuid();
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(
      BuildCommand(reservation.Id, groupReservationId: newGroupId),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    await _availability.Received(1).CheckAsync(
      Arg.Any<IReadOnlyCollection<Guid>>(),
      Arg.Any<DateRange>(),
      Arg.Is<SpotAvailabilityContext>(ctx =>
        ctx.ExcludeReservationId == reservation.Id
        && ctx.AllowGroupOverlap == newGroupId),
      Arg.Any<CancellationToken>());
  }

  [Fact]
  public async Task Handle_UnknownServiceId_ReturnsServiceNotFound()
  {
    DomainReservation reservation = await SeedReservation();
    var missingServiceId = Guid.NewGuid();
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(
      BuildCommand(reservation.Id,
        services: [new ReservationServiceLine(missingServiceId, 1u, 0u, 0u)]),
      CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Reservations.ServiceNotFound");
  }

  [Fact]
  public async Task Handle_DomainEventRaised_OnSuccess()
  {
    DomainReservation reservation = await SeedReservation();
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(BuildCommand(reservation.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation reloaded = await Db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservation.Id);
    reloaded.State.ShouldBe(ReservationState.Confirmed);
    _dispatcher.Dispatched.OfType<ReservationUpdatedDomainEvent>()
      .ShouldContain(ev => ev.ReservationId == reservation.Id);
  }

  [Fact]
  public async Task Handle_DisplayNameSet_PersistsValue()
  {
    DomainReservation reservation = await SeedReservation();
    _availability.CheckAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(
      BuildCommand(reservation.Id) with { DisplayName = "Smith family" },
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation reloaded = await Db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservation.Id);
    reloaded.DisplayName.ShouldBe("Smith family");
  }

  [Fact]
  public async Task Handle_DisplayNameReplaced_OverwritesPrevious()
  {
    DomainReservation reservation = new ReservationBuilder()
      .InState(ReservationState.Confirmed)
      .For(OriginalFrom, OriginalTo)
      .Build();
    reservation.DisplayName = "old";
    Db.Reservations.Add(reservation);
    await Db.SaveChangesAsync();
    _availability.CheckAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(
      BuildCommand(reservation.Id) with { DisplayName = "new" },
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation reloaded = await Db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservation.Id);
    reloaded.DisplayName.ShouldBe("new");
  }

  [Fact]
  public async Task Handle_DisplayNameClearedToNull_RemovesPreviousValue()
  {
    DomainReservation reservation = new ReservationBuilder()
      .InState(ReservationState.Confirmed)
      .For(OriginalFrom, OriginalTo)
      .Build();
    reservation.DisplayName = "old";
    Db.Reservations.Add(reservation);
    await Db.SaveChangesAsync();
    _availability.CheckAsync(default!, default!, default!, default)
      .ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(
      BuildCommand(reservation.Id) with { DisplayName = null },
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation reloaded = await Db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservation.Id);
    reloaded.DisplayName.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_RemovingPaidSpot_ReturnsConflict()
  {
    DomainReservation reservation = await SeedReservation();
    var spotGroupId = Guid.NewGuid();
    var paidSpotId = Guid.NewGuid();
    var paidSpotItemId = Guid.NewGuid();
    Db.SpotGroups.Add(new SpotGroupBuilder().WithId(spotGroupId).WithCapacity(5).Build());
    Db.Spots.Add(new SpotBuilder().WithId(paidSpotId).InGroup(spotGroupId).Build());
    Db.ReservationSpotItems.Add(new DomainReservationSpotItem
    {
      Id = paidSpotItemId,
      ReservationId = reservation.Id,
      SpotGroupId = spotGroupId,
      SpotId = paidSpotId,
      BillId = Guid.NewGuid(),
    });
    await Db.SaveChangesAsync();
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(
      BuildCommand(reservation.Id, spotIds: []),
      CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Reservation.SpotItemPaidCannotBeRemoved");
  }

  [Fact]
  public async Task Handle_RemovingPaidVehicle_ReturnsConflict()
  {
    DomainReservation reservation = await SeedReservation();
    var paidVehicleId = Guid.NewGuid();
    Db.Vehicles.Add(new Vehicle
    {
      Id = paidVehicleId,
      ReservationId = reservation.Id,
      BillId = Guid.NewGuid(),
      RegistrationNumber = "ABC",
    });
    await Db.SaveChangesAsync();
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(
      BuildCommand(reservation.Id, vehicles: []),
      CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Reservation.VehiclePaidCannotBeRemoved");
  }

  [Fact]
  public async Task Handle_CreatedReservationEdited_TransitionsToConfirmed()
  {
    DomainReservation reservation = new ReservationBuilder()
      .InState(ReservationState.Created)
      .For(OriginalFrom, OriginalTo)
      .Build();
    var spotGroupId = Guid.NewGuid();
    var spotId = Guid.NewGuid();
    Db.SpotGroups.Add(new SpotGroupBuilder().WithId(spotGroupId).WithCapacity(5).Build());
    Db.Spots.Add(new SpotBuilder().WithId(spotId).InGroup(spotGroupId).Build());
    Db.Reservations.Add(reservation);
    Db.ReservationSpotItems.Add(new DomainReservationSpotItem
    {
      Id = Guid.NewGuid(),
      ReservationId = reservation.Id,
      SpotGroupId = spotGroupId,
      SpotId = null,
    });
    await Db.SaveChangesAsync();
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(
      BuildCommand(reservation.Id, spotIds: [spotId]),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation reloaded = await Db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservation.Id);
    reloaded.State.ShouldBe(ReservationState.Confirmed);
    DomainReservationSpotItem reloadedItem = await Db.ReservationSpotItems.AsNoTracking()
      .SingleAsync(s => s.ReservationId == reservation.Id);
    reloadedItem.SpotId.ShouldBe(spotId);
  }

  [Fact]
  public async Task Handle_CheckedInReservationEdited_StateRemainsCheckedIn()
  {
    DomainReservation reservation = new ReservationBuilder()
      .InState(ReservationState.CheckedIn)
      .For(OriginalFrom, OriginalTo)
      .Build();
    Db.Reservations.Add(reservation);
    await Db.SaveChangesAsync();
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    Result result = await CreateSut().Handle(BuildCommand(reservation.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation reloaded = await Db.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservation.Id);
    reloaded.State.ShouldBe(ReservationState.CheckedIn);
  }

  [Fact]
  public async Task Handle_FromCreated_RaisesReservationConfirmedDomainEvent()
  {
    DomainReservation seeded = await SeedReservation(state: ReservationState.Created);
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    UpdateReservationCommand command = ValidUpdateCommandFor(seeded);

    Result result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation reservation = await Db.Reservations.AsNoTracking().SingleAsync(r => r.Id == seeded.Id);
    reservation.State.ShouldBe(ReservationState.Confirmed);
    _dispatcher.Dispatched.OfType<ReservationConfirmedDomainEvent>()
      .ShouldContain(ev => ev.ReservationId == seeded.Id);
  }

  [Fact]
  public async Task Handle_FromConfirmed_DoesNotRaiseReservationConfirmedDomainEvent()
  {
    DomainReservation seeded = await SeedReservation(state: ReservationState.Confirmed);
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    UpdateReservationCommand command = ValidUpdateCommandFor(seeded);

    Result result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    _dispatcher.Dispatched.OfType<ReservationConfirmedDomainEvent>()
      .ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_UpdatesLanguage()
  {
    DomainReservation seeded = await SeedReservation(state: ReservationState.Confirmed);
    _availability.CheckAsync(default!, default!, default!, default).ReturnsForAnyArgs(Result.Success());

    UpdateReservationCommand command = ValidUpdateCommandFor(seeded) with { Language = ReservationLanguages.English };

    Result result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation reservation = await Db.Reservations.AsNoTracking().SingleAsync(r => r.Id == seeded.Id);
    reservation.Language.ShouldBe(ReservationLanguages.English);
  }
}
