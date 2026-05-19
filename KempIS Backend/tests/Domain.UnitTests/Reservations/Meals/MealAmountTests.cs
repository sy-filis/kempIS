using Domain.Reservations.Meals;

namespace Domain.UnitTests.Reservations.Meals;

public sealed class MealAmountTests
{
  [Fact]
  public void Empty_AllBucketsZero_AtIsNull()
  {
    MealAmount empty = MealAmount.Empty;

    empty.At.ShouldBeNull();
    empty.Normal.ShouldBe(0u);
    empty.GlutenFree.ShouldBe(0u);
    empty.LactoseFree.ShouldBe(0u);
    empty.Vegetarian.ShouldBe(0u);
    empty.GlutenFreeLactoseFree.ShouldBe(0u);
    empty.GlutenFreeVegetarian.ShouldBe(0u);
    empty.LactoseFreeVegetarian.ShouldBe(0u);
    empty.GlutenFreeLactoseFreeVegetarian.ShouldBe(0u);
    empty.Total.ShouldBe(0u);
  }

  [Fact]
  public void Total_SumsAllEightBuckets_IgnoresAt()
  {
    MealAmount amount = new()
    {
      At = new TimeOnly(8, 30),
      Normal = 1,
      GlutenFree = 2,
      LactoseFree = 3,
      Vegetarian = 4,
      GlutenFreeLactoseFree = 5,
      GlutenFreeVegetarian = 6,
      LactoseFreeVegetarian = 7,
      GlutenFreeLactoseFreeVegetarian = 8,
    };

    amount.Total.ShouldBe(36u);
  }

  [Fact]
  public void With_OverridesSingleBucket_OtherFieldsUnchanged()
  {
    MealAmount baseline = MealAmount.Empty with { Normal = 5, At = new TimeOnly(12, 0) };

    MealAmount updated = baseline with { GlutenFree = 2 };

    updated.Normal.ShouldBe(5u);
    updated.GlutenFree.ShouldBe(2u);
    updated.At.ShouldBe(new TimeOnly(12, 0));
    updated.Total.ShouldBe(7u);
  }
}
