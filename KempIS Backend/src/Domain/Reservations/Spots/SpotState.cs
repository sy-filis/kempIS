namespace Domain.Reservations.Spots;

public enum SpotState
{
  Unoccupied,
  Occupied,
  ExpectingArrival,
  ExpectingDeparture,
  OutOfOrder
}
