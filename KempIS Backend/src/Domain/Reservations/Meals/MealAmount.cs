namespace Domain.Reservations.Meals;

public sealed record MealAmount
{
  public TimeOnly? At { get; init; }
  public uint Normal { get; init; }
  public uint GlutenFree { get; init; }
  public uint LactoseFree { get; init; }
  public uint Vegetarian { get; init; }
  public uint GlutenFreeLactoseFree { get; init; }
  public uint GlutenFreeVegetarian { get; init; }
  public uint LactoseFreeVegetarian { get; init; }
  public uint GlutenFreeLactoseFreeVegetarian { get; init; }

  public uint Total =>
    Normal + GlutenFree + LactoseFree + Vegetarian +
    GlutenFreeLactoseFree + GlutenFreeVegetarian +
    LactoseFreeVegetarian + GlutenFreeLactoseFreeVegetarian;

  public static MealAmount Empty { get; } = new();
}
