using Application.Reservations.Meals;
using Domain.Reservations.Meals;
using SharedKernel;

namespace Application.UnitTests.Reservations.Meals;

public sealed class GetMealTotalsInRangeQueryHandlerTests : HandlerTestBase
{
  private GetMealTotalsInRangeQueryHandler CreateSut() => new(Db);

  [Fact]
  public async Task Handle_SumsBucketsAcrossReservations_OnTheSameDate()
  {
    var date = new DateOnly(2026, 5, 3);
    Db.Meals.Add(new Meal
    {
      ReservationId = Guid.NewGuid(),
      Date = date,
      Breakfast = MealAmount.Empty with { Normal = 3, GlutenFree = 1 },
      Lunch = MealAmount.Empty with { Vegetarian = 2 },
    });
    Db.Meals.Add(new Meal
    {
      ReservationId = Guid.NewGuid(),
      Date = date,
      Breakfast = MealAmount.Empty with { Normal = 5, GlutenFreeVegetarian = 4 },
      Lunch = MealAmount.Empty with { Vegetarian = 1 },
    });
    await Db.SaveChangesAsync();

    Result<List<MealTotalsResponse>> result = await CreateSut().Handle(
      new GetMealTotalsInRangeQuery(date, date),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    MealTotalsResponse row = result.Value.ShouldHaveSingleItem();
    row.Date.ShouldBe(date);
    row.Breakfast.Normal.ShouldBe(8u);
    row.Breakfast.GlutenFree.ShouldBe(1u);
    row.Breakfast.GlutenFreeVegetarian.ShouldBe(4u);
    row.Lunch.Vegetarian.ShouldBe(3u);
    row.LunchPackage.Normal.ShouldBe(0u);
    row.Dinner.Normal.ShouldBe(0u);
  }

  [Fact]
  public async Task Handle_GroupsByDate_OmitsDatesWithoutMeals_OrdersAscending()
  {
    Db.Meals.Add(new Meal
    {
      ReservationId = Guid.NewGuid(),
      Date = new DateOnly(2026, 5, 3),
      Breakfast = MealAmount.Empty with { Normal = 1 },
    });
    Db.Meals.Add(new Meal
    {
      ReservationId = Guid.NewGuid(),
      Date = new DateOnly(2026, 5, 1),
      Breakfast = MealAmount.Empty with { Normal = 2 },
    });
    await Db.SaveChangesAsync();

    Result<List<MealTotalsResponse>> result = await CreateSut().Handle(
      new GetMealTotalsInRangeQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(2);
    result.Value[0].Date.ShouldBe(new DateOnly(2026, 5, 1));
    result.Value[0].Breakfast.Normal.ShouldBe(2u);
    result.Value[1].Date.ShouldBe(new DateOnly(2026, 5, 3));
    result.Value[1].Breakfast.Normal.ShouldBe(1u);
  }

  [Fact]
  public async Task Handle_ExcludesRowsOutsideRange()
  {
    Db.Meals.Add(new Meal
    {
      ReservationId = Guid.NewGuid(),
      Date = new DateOnly(2026, 4, 30),
      Breakfast = MealAmount.Empty with { Normal = 9 },
    });
    Db.Meals.Add(new Meal
    {
      ReservationId = Guid.NewGuid(),
      Date = new DateOnly(2026, 5, 6),
      Breakfast = MealAmount.Empty with { Normal = 9 },
    });
    Db.Meals.Add(new Meal
    {
      ReservationId = Guid.NewGuid(),
      Date = new DateOnly(2026, 5, 1),
      Breakfast = MealAmount.Empty with { Normal = 2 },
    });
    await Db.SaveChangesAsync();

    Result<List<MealTotalsResponse>> result = await CreateSut().Handle(
      new GetMealTotalsInRangeQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
      CancellationToken.None);

    result.Value.ShouldHaveSingleItem().Breakfast.Normal.ShouldBe(2u);
  }
}
