using Domain.Common;
using SharedKernel;

namespace Domain.Reservations.GroupReservations;

public sealed class GroupReservation : Entity
{
  public Guid Id { get; set; }

  public string Number { get; set; }

  public GroupReservationState State { get; set; }

  public DateRange Period { get; set; }

  public string Secret { get; set; }

  public string OrganizerName { get; set; }

  public string OrganizerEmail { get; set; }

  public string OrganizerPhone { get; set; }

  public string? DisplayName { get; set; }

  public DateTime CreatedAtUtc { get; set; }

  public DateTime? UpdatedAtUtc { get; set; }

  public string? Note { get; set; }

  public string Language { get; set; } = ReservationLanguages.Czech;

  public List<GroupReservationSpot> HeldSpots { get; set; } = [];
}
