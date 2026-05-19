using Domain.Common;
using Domain.Reservations.ReservationMakers;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.ReservationStates;
using SharedKernel;
using CheckInStatusEnum = Domain.Reservations.Reservations.OnlineCheckInStatus;

namespace Domain.Reservations.Reservations;

public sealed class Reservation : Entity
{
  public Guid Id { get; set; }

  /// <summary>Format: R-{year}/{seq:D4}.</summary>
  public string Number { get; set; }

  public string? DisplayName { get; set; }

  public ReservationMaker ReservationMaker { get; set; }

  public Guid? GroupReservationId { get; set; }

  public DateRange Period { get; set; }

  public ReservationState State { get; set; }

  public DateTime CreatedAtUtc { get; set; }

  public DateTime? UpdatedAtUtc { get; set; }

  public string? Note { get; set; }

  public string Secret { get; set; }

  public OnlineCheckInStatus OnlineCheckInStatus { get; set; } = OnlineCheckInStatus.None;

  public string Language { get; set; } = ReservationLanguages.Czech;

  public Result SubmitOnlineCheckIn()
  {
    if (OnlineCheckInStatus == CheckInStatusEnum.Completed)
    {
      return Result.Failure(ReservationErrors.AlreadyOnlineCheckedIn(Id));
    }
    OnlineCheckInStatus = CheckInStatusEnum.Completed;
    return Result.Success();
  }

  public Result MarkCompletedIfAllKeysReturned(IReadOnlyCollection<ReservationSpotItem> spotItems)
  {
    if (State != ReservationState.CheckedIn)
    { return Result.Success(); }
    if (spotItems.Count == 0)
    { return Result.Success(); }
    if (!spotItems.All(rsi => rsi.HasReturnedKeys))
    { return Result.Success(); }

    State = ReservationState.Completed;
    return Result.Success();
  }

  public void ConfirmIfCreated()
  {
    if (State == ReservationState.Created)
    {
      State = ReservationState.Confirmed;
      Raise(new ReservationConfirmedDomainEvent(Id));
    }
  }
}
