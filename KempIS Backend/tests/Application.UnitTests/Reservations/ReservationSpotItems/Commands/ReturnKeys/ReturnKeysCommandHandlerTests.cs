using Application.Reservations.ReservationSpotItems.Commands.ReturnKeys;
using Domain.Reservations;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.ReservationStates;
using SharedKernel;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Application.UnitTests.Reservations.ReservationSpotItems.Commands.ReturnKeys;

public sealed class ReturnKeysCommandHandlerTests : HandlerTestBase
{
  private ReturnKeysCommandHandler CreateSut() => new(Db, Clock);

  private async Task<DomainReservation> SeedReservation(ReservationState state)
  {
    DomainReservation r = new ReservationBuilder().InState(state).Build();
    Db.Reservations.Add(r);
    await Db.SaveChangesAsync();
    return r;
  }

  private async Task<ReservationSpotItem> SeedItem(Guid reservationId, bool hasReturnedKeys = false)
  {
    ReservationSpotItem item = new()
    {
      Id = Guid.NewGuid(),
      ReservationId = reservationId,
      SpotGroupId = Guid.NewGuid(),
      SpotId = Guid.NewGuid(),
      HasReturnedKeys = hasReturnedKeys,
    };
    Db.ReservationSpotItems.Add(item);
    await Db.SaveChangesAsync();
    return item;
  }

  [Fact]
  public async Task Handle_ItemNotFound_ReturnsNotFound()
  {
    var missing = Guid.NewGuid();

    Result result = await CreateSut().Handle(new ReturnKeysCommand(missing), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationSpotItemErrors.NotFound(missing));
  }

  [Fact]
  public async Task Handle_ReservationNotFound_ReturnsReservationNotFound()
  {
    // Orphan item: references a reservation id that doesn't exist (FK enforcement is OFF in tests).
    ReservationSpotItem orphan = new()
    {
      Id = Guid.NewGuid(),
      ReservationId = Guid.NewGuid(),
      SpotGroupId = Guid.NewGuid(),
      SpotId = Guid.NewGuid(),
      HasReturnedKeys = false,
    };
    Db.ReservationSpotItems.Add(orphan);
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(new ReturnKeysCommand(orphan.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.NotFound(orphan.ReservationId));
  }

  [Fact]
  public async Task Handle_ReservationNotCheckedIn_FirstReturn_Fails()
  {
    DomainReservation r = await SeedReservation(ReservationState.Confirmed);
    ReservationSpotItem item = await SeedItem(r.Id);

    Result result = await CreateSut().Handle(new ReturnKeysCommand(item.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.CannotReturnKeysReservationNotCheckedIn(r.Id));
    ReservationSpotItem reloaded = await Db.ReservationSpotItems.AsNoTracking().SingleAsync(x => x.Id == item.Id);
    reloaded.HasReturnedKeys.ShouldBeFalse();
  }

  [Fact]
  public async Task Handle_LastItemReturned_TransitionsReservationToCompleted()
  {
    DomainReservation r = await SeedReservation(ReservationState.CheckedIn);
    _ = await SeedItem(r.Id, hasReturnedKeys: true);
    ReservationSpotItem last = await SeedItem(r.Id);
    DateTime now = new(2026, 5, 5, 11, 0, 0, DateTimeKind.Utc);
    Clock.Set(now);

    Result result = await CreateSut().Handle(new ReturnKeysCommand(last.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation reloaded = await Db.Reservations.AsNoTracking().SingleAsync(x => x.Id == r.Id);
    reloaded.State.ShouldBe(ReservationState.Completed);
    reloaded.UpdatedAtUtc.ShouldBe(now);
    ReservationSpotItem reloadedLast = await Db.ReservationSpotItems.AsNoTracking().SingleAsync(x => x.Id == last.Id);
    reloadedLast.HasReturnedKeys.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_NotLastItem_LeavesReservationCheckedIn()
  {
    DomainReservation r = await SeedReservation(ReservationState.CheckedIn);
    ReservationSpotItem item = await SeedItem(r.Id);
    await SeedItem(r.Id);  // second sibling, still false

    Result result = await CreateSut().Handle(new ReturnKeysCommand(item.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation reloaded = await Db.Reservations.AsNoTracking().SingleAsync(x => x.Id == r.Id);
    reloaded.State.ShouldBe(ReservationState.CheckedIn);
  }

  [Fact]
  public async Task Handle_AlreadyReturnedReservationCompleted_IsIdempotentSuccess()
  {
    DomainReservation r = await SeedReservation(ReservationState.Completed);
    ReservationSpotItem item = await SeedItem(r.Id, hasReturnedKeys: true);

    Result result = await CreateSut().Handle(new ReturnKeysCommand(item.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation reloaded = await Db.Reservations.AsNoTracking().SingleAsync(x => x.Id == r.Id);
    reloaded.State.ShouldBe(ReservationState.Completed);
    ReservationSpotItem reloadedItem = await Db.ReservationSpotItems.AsNoTracking().SingleAsync(x => x.Id == item.Id);
    reloadedItem.HasReturnedKeys.ShouldBeTrue();
  }
}
