using Application.Reservations.Commands.CheckInReservation;
using Domain.Reservations;
using Domain.Reservations.ReservationStates;
using SharedKernel;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Application.UnitTests.Reservations.Commands.CheckInReservation;

public sealed class CheckInReservationCommandHandlerTests : HandlerTestBase
{
  private CheckInReservationCommandHandler CreateSut() => new(Db, Clock);

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
  public async Task Handle_FromCreatedOrConfirmed_TransitionsToCheckedIn(ReservationState state)
  {
    DomainReservation r = await SeedReservation(state);
    var checkInAt = new DateTime(2026, 7, 1, 14, 0, 0, DateTimeKind.Utc);
    Clock.Set(checkInAt);

    Result result = await CreateSut().Handle(
        new CheckInReservationCommand(r.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation reloaded = await Db.Reservations.AsNoTracking().SingleAsync(x => x.Id == r.Id);
    reloaded.State.ShouldBe(ReservationState.CheckedIn);
    reloaded.UpdatedAtUtc.ShouldBe(checkInAt);
  }

  [Fact]
  public async Task Handle_ReservationMissing_ReturnsNotFound()
  {
    var missing = Guid.NewGuid();

    Result result = await CreateSut().Handle(
        new CheckInReservationCommand(missing), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.NotFound(missing));
  }

  [Theory]
  [InlineData(ReservationState.CheckedIn)]
  [InlineData(ReservationState.Cancelled)]
  [InlineData(ReservationState.Completed)]
  public async Task Handle_FromInvalidState_ReturnsInvalidStateForCheckIn(ReservationState state)
  {
    DomainReservation r = await SeedReservation(state);

    Result result = await CreateSut().Handle(
        new CheckInReservationCommand(r.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.InvalidStateForCheckIn(r.Id));
  }
}
