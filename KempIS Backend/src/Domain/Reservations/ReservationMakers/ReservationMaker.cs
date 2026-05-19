using SharedKernel;

namespace Domain.Reservations.ReservationMakers;

public sealed record ReservationMaker(
  string Name,
  string Surname,
  string Email,
  string Phone
);
