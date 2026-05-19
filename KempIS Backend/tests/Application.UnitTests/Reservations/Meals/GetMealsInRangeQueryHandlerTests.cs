using Application.Reservations.Meals;
using Domain.Reservations.Meals;
using SharedKernel;

namespace Application.UnitTests.Reservations.Meals;

public sealed class GetMealsInRangeQueryHandlerTests : HandlerTestBase
{
  private GetMealsInRangeQueryHandler CreateSut() => new(Db);

  [Fact]
  public async Task Handle_ReturnsMealsFromMultipleReservations_InRange()
  {
    var r1 = Guid.NewGuid();
    var r2 = Guid.NewGuid();
    Db.Meals.Add(new Meal
    {
      ReservationId = r1,
      Date = new DateOnly(2026, 5, 2),
      Breakfast = MealAmount.Empty with { Normal = 1 },
    });
    Db.Meals.Add(new Meal
    {
      ReservationId = r2,
      Date = new DateOnly(2026, 5, 3),
      Dinner = MealAmount.Empty with { GlutenFree = 2 },
    });
    await Db.SaveChangesAsync();

    Result<List<MealResponse>> result = await CreateSut().Handle(
      new GetMealsInRangeQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(2);
    result.Value.ShouldContain(m => m.ReservationId == r1 && m.Breakfast.Normal == 1);
    result.Value.ShouldContain(m => m.ReservationId == r2 && m.Dinner.GlutenFree == 2);
  }

  [Fact]
  public async Task Handle_IncludesRowsOnBothBoundaries_AndExcludesOutside()
  {
    var reservationId = Guid.NewGuid();
    Db.Meals.Add(new Meal
    {
      ReservationId = reservationId,
      Date = new DateOnly(2026, 5, 1),
      Breakfast = MealAmount.Empty with { Normal = 1 },
    });
    Db.Meals.Add(new Meal
    {
      ReservationId = reservationId,
      Date = new DateOnly(2026, 5, 5),
      Breakfast = MealAmount.Empty with { Normal = 2 },
    });
    Db.Meals.Add(new Meal
    {
      ReservationId = reservationId,
      Date = new DateOnly(2026, 4, 30),
      Breakfast = MealAmount.Empty with { Normal = 3 },
    });
    Db.Meals.Add(new Meal
    {
      ReservationId = reservationId,
      Date = new DateOnly(2026, 5, 6),
      Breakfast = MealAmount.Empty with { Normal = 4 },
    });
    await Db.SaveChangesAsync();

    Result<List<MealResponse>> result = await CreateSut().Handle(
      new GetMealsInRangeQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
      CancellationToken.None);

    result.Value.Count.ShouldBe(2);
    result.Value.ShouldAllBe(m => m.Date >= new DateOnly(2026, 5, 1) && m.Date <= new DateOnly(2026, 5, 5));
  }

  [Fact]
  public async Task Handle_ReturnsEmpty_WhenNoMealsInRange()
  {
    Db.Meals.Add(new Meal
    {
      ReservationId = Guid.NewGuid(),
      Date = new DateOnly(2026, 4, 30),
      Breakfast = MealAmount.Empty with { Normal = 1 },
    });
    await Db.SaveChangesAsync();

    Result<List<MealResponse>> result = await CreateSut().Handle(
      new GetMealsInRangeQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
      CancellationToken.None);

    result.Value.ShouldBeEmpty();
  }
}
