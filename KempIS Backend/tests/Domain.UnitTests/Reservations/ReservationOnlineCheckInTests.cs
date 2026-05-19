using Domain.Common;
using Domain.Reservations.ReservationMakers;
using Domain.Reservations.Reservations;
using Domain.Reservations.ReservationStates;
using SharedKernel;

namespace Domain.UnitTests.Reservations;

public sealed class ReservationOnlineCheckInTests
{
  [Fact]
  public void SubmitOnlineCheckIn_WhenStatusIsNone_TransitionsToCompleted()
  {
    Reservation reservation = BuildReservation();
    reservation.OnlineCheckInStatus.ShouldBe(OnlineCheckInStatus.None);

    Result result = reservation.SubmitOnlineCheckIn();

    result.IsSuccess.ShouldBeTrue();
    reservation.OnlineCheckInStatus.ShouldBe(OnlineCheckInStatus.Completed);
  }

  [Fact]
  public void SubmitOnlineCheckIn_WhenAlreadyCompleted_ReturnsAlreadyOnlineCheckedIn()
  {
    Reservation reservation = BuildReservation();
    reservation.SubmitOnlineCheckIn();

    Result result = reservation.SubmitOnlineCheckIn();

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Reservations.AlreadyOnlineCheckedIn");
  }

  private static Reservation BuildReservation()
  {
    return new Reservation
    {
      Id = Guid.NewGuid(),
      Number = "R-TEST/0001",
      Period = new DateRange(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5)),
      State = ReservationState.Created,
      Secret = "test-secret",
      CreatedAtUtc = new DateTime(2026, 4, 24, 12, 0, 0, DateTimeKind.Utc),
      ReservationMaker = new ReservationMaker("Alice", "Test", "a@b.c", "+420000000000")
    };
  }
}
