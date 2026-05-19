using SharedKernel;

namespace Domain.Reservations.Meals;

public static class MealErrors
{
  public static Error DateOutsideReservationPeriod(DateOnly date, Guid reservationId) => Error.Problem(
      "Meals.DateOutsideReservationPeriod",
      $"The Meal date '{date}' is outside the period of Reservation '{reservationId}'.");
}
