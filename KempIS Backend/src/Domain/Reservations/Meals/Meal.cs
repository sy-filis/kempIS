using SharedKernel;

namespace Domain.Reservations.Meals;

public sealed class Meal : Entity
{
  public Guid ReservationId { get; set; }

  public DateOnly Date { get; set; }

  public MealAmount Breakfast { get; set; } = new();

  public MealAmount Lunch { get; set; } = new();

  public MealAmount LunchPackage { get; set; } = new();

  public MealAmount Dinner { get; set; } = new();
}
