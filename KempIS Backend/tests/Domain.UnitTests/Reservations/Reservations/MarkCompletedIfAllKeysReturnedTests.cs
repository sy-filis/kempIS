using Domain.Reservations.Reservations;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.ReservationStates;
using SharedKernel;

namespace Domain.UnitTests.Reservations.Reservations;

public sealed class MarkCompletedIfAllKeysReturnedTests
{
  private static Reservation MakeReservation(ReservationState state) => new()
  {
    Id = Guid.NewGuid(),
    Number = "R-2026/0001",
    Period = new Domain.Common.DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
    State = state,
    ReservationMaker = new Domain.Reservations.ReservationMakers.ReservationMaker("A", "B", "a@b", "+420"),
    Secret = new string('0', 64),
    CreatedAtUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
  };

  private static ReservationSpotItem Item(bool returned) => new()
  {
    Id = Guid.NewGuid(),
    ReservationId = Guid.NewGuid(),
    SpotGroupId = Guid.NewGuid(),
    SpotId = Guid.NewGuid(),
    HasReturnedKeys = returned,
  };

  [Fact]
  public void NotCheckedIn_AllReturned_StateUnchanged()
  {
    Reservation r = MakeReservation(ReservationState.Confirmed);

    Result result = r.MarkCompletedIfAllKeysReturned([Item(returned: true)]);

    result.IsSuccess.ShouldBeTrue();
    r.State.ShouldBe(ReservationState.Confirmed);
  }

  [Fact]
  public void CheckedIn_AllReturned_TransitionsToCompleted()
  {
    Reservation r = MakeReservation(ReservationState.CheckedIn);

    Result result = r.MarkCompletedIfAllKeysReturned([Item(true), Item(true)]);

    result.IsSuccess.ShouldBeTrue();
    r.State.ShouldBe(ReservationState.Completed);
  }

  [Fact]
  public void CheckedIn_OneNotReturned_StaysCheckedIn()
  {
    Reservation r = MakeReservation(ReservationState.CheckedIn);

    Result result = r.MarkCompletedIfAllKeysReturned([Item(true), Item(false)]);

    result.IsSuccess.ShouldBeTrue();
    r.State.ShouldBe(ReservationState.CheckedIn);
  }

  [Fact]
  public void CheckedIn_NoSpotItems_NoOp()
  {
    Reservation r = MakeReservation(ReservationState.CheckedIn);

    Result result = r.MarkCompletedIfAllKeysReturned([]);

    result.IsSuccess.ShouldBeTrue();
    r.State.ShouldBe(ReservationState.CheckedIn);
  }

  [Fact]
  public void AlreadyCompleted_StateUnchanged()
  {
    Reservation r = MakeReservation(ReservationState.Completed);

    Result result = r.MarkCompletedIfAllKeysReturned([Item(true)]);

    result.IsSuccess.ShouldBeTrue();
    r.State.ShouldBe(ReservationState.Completed);
  }
}
