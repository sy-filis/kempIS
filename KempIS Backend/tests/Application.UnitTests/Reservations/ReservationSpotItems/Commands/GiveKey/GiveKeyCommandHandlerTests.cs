using Application.Reservations.ReservationSpotItems.Commands.GiveKey;
using Domain.Reservations;
using Domain.Reservations.Reservations;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.ReservationStates;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using TestUtilities.Builders;

namespace Application.UnitTests.Reservations.ReservationSpotItems.Commands.GiveKey;

public sealed class GiveKeyCommandHandlerTests : HandlerTestBase
{
  private GiveKeyCommandHandler CreateSut() => new(Db, Clock);

  private async Task<Guid> SeedReservationWithSpotItem(ReservationState state)
  {
    Reservation reservation = new ReservationBuilder().InState(state).Build();
    Db.Reservations.Add(reservation);

    var spotItem = new ReservationSpotItem
    {
      Id = Guid.NewGuid(),
      ReservationId = reservation.Id,
      SpotGroupId = Guid.NewGuid(),
      SpotId = Guid.NewGuid(),
    };
    Db.ReservationSpotItems.Add(spotItem);

    await Db.SaveChangesAsync();
    return spotItem.Id;
  }

  [Fact]
  public async Task Handle_SetsHasGivenKey_WhenReservationConfirmed()
  {
    Guid id = await SeedReservationWithSpotItem(ReservationState.Confirmed);

    Result result = await CreateSut().Handle(new GiveKeyCommand(id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    ReservationSpotItem stored = (await Db.ReservationSpotItems.AsNoTracking().FirstOrDefaultAsync(rsi => rsi.Id == id))!;
    stored.HasGivenKey.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_SetsHasGivenKey_WhenReservationCheckedIn()
  {
    Guid id = await SeedReservationWithSpotItem(ReservationState.CheckedIn);

    Result result = await CreateSut().Handle(new GiveKeyCommand(id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    ReservationSpotItem stored = (await Db.ReservationSpotItems.AsNoTracking().FirstOrDefaultAsync(rsi => rsi.Id == id))!;
    stored.HasGivenKey.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_IsIdempotent_WhenAlreadyGivenAndReservationCompleted()
  {
    // Even though the reservation is Completed (which the gate would reject),
    // an already-given flag means no transition happens, so the call succeeds.
    Reservation reservation = new ReservationBuilder().InState(ReservationState.Completed).Build();
    Db.Reservations.Add(reservation);
    var spotItem = new ReservationSpotItem
    {
      Id = Guid.NewGuid(),
      ReservationId = reservation.Id,
      SpotGroupId = Guid.NewGuid(),
      SpotId = Guid.NewGuid(),
      HasGivenKey = true,
    };
    Db.ReservationSpotItems.Add(spotItem);
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(new GiveKeyCommand(spotItem.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    ReservationSpotItem stored = (await Db.ReservationSpotItems.AsNoTracking().FirstOrDefaultAsync(rsi => rsi.Id == spotItem.Id))!;
    stored.HasGivenKey.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_ReturnsFailure_WhenReservationCancelled()
  {
    Guid id = await SeedReservationWithSpotItem(ReservationState.Cancelled);

    Result result = await CreateSut().Handle(new GiveKeyCommand(id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationSpotItemErrors.CannotGiveKeyReservationNotConfirmedOrCheckedIn);
  }

  [Fact]
  public async Task Handle_ReturnsFailure_WhenReservationCompletedAndFlagNotSet()
  {
    Guid id = await SeedReservationWithSpotItem(ReservationState.Completed);

    Result result = await CreateSut().Handle(new GiveKeyCommand(id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationSpotItemErrors.CannotGiveKeyReservationNotConfirmedOrCheckedIn);
  }

  [Fact]
  public async Task Handle_ReturnsNotFound_WhenSpotItemMissing()
  {
    var givenId = Guid.NewGuid();

    Result result = await CreateSut().Handle(new GiveKeyCommand(givenId), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationSpotItemErrors.NotFound(givenId));
  }

  [Fact]
  public async Task Handle_ReservationMissing_ReturnsReservationNotFound()
  {
    // Orphan item: references a reservation id that does not exist.
    // FK enforcement is OFF in HandlerTestBase so this is allowed.
    var orphan = new ReservationSpotItem
    {
      Id = Guid.NewGuid(),
      ReservationId = Guid.NewGuid(),
      SpotGroupId = Guid.NewGuid(),
      SpotId = Guid.NewGuid(),
      HasGivenKey = false,
    };
    Db.ReservationSpotItems.Add(orphan);
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(new GiveKeyCommand(orphan.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.NotFound(orphan.ReservationId));
  }
}
