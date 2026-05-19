using Domain.Reservations;
using Domain.Reservations.Reservations;
using Domain.Reservations.ReservationStates;

namespace Domain.UnitTests.Reservations.Reservations;

public sealed class ConfirmIfCreatedTests
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

  [Fact]
  public void Created_TransitionsToConfirmed()
  {
    Reservation r = MakeReservation(ReservationState.Created);

    r.ConfirmIfCreated();

    r.State.ShouldBe(ReservationState.Confirmed);
  }

  [Fact]
  public void Confirmed_StateUnchanged()
  {
    Reservation r = MakeReservation(ReservationState.Confirmed);

    r.ConfirmIfCreated();

    r.State.ShouldBe(ReservationState.Confirmed);
  }

  [Fact]
  public void CheckedIn_StateUnchanged()
  {
    Reservation r = MakeReservation(ReservationState.CheckedIn);

    r.ConfirmIfCreated();

    r.State.ShouldBe(ReservationState.CheckedIn);
  }

  [Fact]
  public void Completed_StateUnchanged()
  {
    Reservation r = MakeReservation(ReservationState.Completed);

    r.ConfirmIfCreated();

    r.State.ShouldBe(ReservationState.Completed);
  }

  [Fact]
  public void Cancelled_StateUnchanged()
  {
    Reservation r = MakeReservation(ReservationState.Cancelled);

    r.ConfirmIfCreated();

    r.State.ShouldBe(ReservationState.Cancelled);
  }

  [Fact]
  public void ConfirmIfCreated_FromCreated_RaisesReservationConfirmedDomainEvent()
  {
    Reservation reservation = MakeReservation(ReservationState.Created);

    reservation.ConfirmIfCreated();

    ReservationConfirmedDomainEvent? evt =
      reservation.DomainEvents.OfType<ReservationConfirmedDomainEvent>().SingleOrDefault();
    evt.ShouldNotBeNull();
    evt.ReservationId.ShouldBe(reservation.Id);
  }

  [Fact]
  public void ConfirmIfCreated_FromConfirmed_DoesNotRaiseReservationConfirmedDomainEvent()
  {
    Reservation reservation = MakeReservation(ReservationState.Confirmed);

    reservation.ConfirmIfCreated();

    reservation.DomainEvents.OfType<ReservationConfirmedDomainEvent>().ShouldBeEmpty();
  }

  [Fact]
  public void ConfirmIfCreated_FromCheckedIn_DoesNotRaiseReservationConfirmedDomainEvent()
  {
    Reservation reservation = MakeReservation(ReservationState.CheckedIn);

    reservation.ConfirmIfCreated();

    reservation.DomainEvents.OfType<ReservationConfirmedDomainEvent>().ShouldBeEmpty();
  }

  [Fact]
  public void ConfirmIfCreated_FromCancelled_DoesNotRaiseReservationConfirmedDomainEvent()
  {
    Reservation reservation = MakeReservation(ReservationState.Cancelled);

    reservation.ConfirmIfCreated();

    reservation.DomainEvents.OfType<ReservationConfirmedDomainEvent>().ShouldBeEmpty();
  }

  [Fact]
  public void ConfirmIfCreated_FromCompleted_DoesNotRaiseReservationConfirmedDomainEvent()
  {
    Reservation reservation = MakeReservation(ReservationState.Completed);

    reservation.ConfirmIfCreated();

    reservation.DomainEvents.OfType<ReservationConfirmedDomainEvent>().ShouldBeEmpty();
  }
}
