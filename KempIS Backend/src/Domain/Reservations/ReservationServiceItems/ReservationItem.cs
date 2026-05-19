using Domain.Finance;
using Domain.Operations;
using SharedKernel;

namespace Domain.Reservations.ReservationServiceItems;

public sealed class ReservationServiceItem : Entity
{
  public Guid Id { get; set; }
  public Guid ReservationId { get; set; }
  public Guid ServiceId { get; set; }
  public uint Quantity { get; set; }
  public uint RecapSingleQuantity { get; set; }
  public uint RecapDayQuantity { get; set; }
}
