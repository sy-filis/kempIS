using SharedKernel;

namespace Domain.Reservations.ReservationSpotItems;

public class ReservationSpotItem : Entity
{
  public Guid Id { get; set; }
  public Guid ReservationId { get; set; }
  public Guid SpotGroupId { get; set; }
  public Guid? SpotId { get; set; }
  public bool HasGivenKey { get; set; }
  public bool HasReturnedKeys { get; set; }
  public Guid? BillId { get; set; }
}
