using SharedKernel;

namespace Domain.Reservations.Vehicles;

public sealed class Vehicle : Entity
{
  public Guid Id { get; set; }

  public Guid? ReservationId { get; set; }

  public Guid? BillId { get; set; }

  public Guid? ServiceId { get; set; }

  public string RegistrationNumber { get; set; }
}
