using Application.Reservations.Commands.CancelReservation;
using Domain.Reservations;
using Domain.Reservations.ReservationStates;
using SharedKernel;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Application.UnitTests.Reservations.Commands.CancelReservation;

public sealed class CancelReservationCommandHandlerTests : HandlerTestBase
{
  private CancelReservationCommandHandler CreateSut() => new(Db, Clock);

  private async Task<DomainReservation> SeedReservation(ReservationState state)
  {
    DomainReservation r = new ReservationBuilder().InState(state).Build();
    Db.Reservations.Add(r);
    await Db.SaveChangesAsync();
    return r;
  }

  [Theory]
  [InlineData(ReservationState.Created)]
  [InlineData(ReservationState.Confirmed)]
  [InlineData(ReservationState.CheckedIn)]
  [InlineData(ReservationState.Completed)]
  public async Task Handle_FromAnyNonCancelledState_TransitionsToCancelled(ReservationState state)
  {
    DomainReservation r = await SeedReservation(state);
    var cancelAt = new DateTime(2026, 5, 21, 9, 0, 0, DateTimeKind.Utc);
    Clock.Set(cancelAt);

    Result result = await CreateSut().Handle(
        new CancelReservationCommand(r.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation reloaded = await Db.Reservations.AsNoTracking().SingleAsync(x => x.Id == r.Id);
    reloaded.State.ShouldBe(ReservationState.Cancelled);
    reloaded.UpdatedAtUtc.ShouldBe(cancelAt);
  }

  [Fact]
  public async Task Handle_ReservationMissing_ReturnsNotFound()
  {
    var missing = Guid.NewGuid();

    Result result = await CreateSut().Handle(
        new CancelReservationCommand(missing), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.NotFound(missing));
  }

  [Fact]
  public async Task Handle_AlreadyCancelled_ReturnsAlreadyCancelled_AndDoesNotUpdateTimestamp()
  {
    var originalUpdate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    DomainReservation r = new ReservationBuilder()
      .InState(ReservationState.Cancelled)
      .UpdatedAt(originalUpdate)
      .Build();
    Db.Reservations.Add(r);
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(
        new CancelReservationCommand(r.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.AlreadyCancelled(r.Id));
    DomainReservation reloaded = await Db.Reservations.AsNoTracking().SingleAsync(x => x.Id == r.Id);
    reloaded.UpdatedAtUtc.ShouldBe(originalUpdate);
  }
}
