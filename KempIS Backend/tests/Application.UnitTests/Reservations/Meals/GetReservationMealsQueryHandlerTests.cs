using Application.Reservations.Meals;
using Domain.Reservations.Meals;
using SharedKernel;

namespace Application.UnitTests.Reservations.Meals;

public sealed class GetReservationMealsQueryHandlerTests : HandlerTestBase
{
  private GetReservationMealsQueryHandler CreateSut() => new(Db);

  [Fact]
  public async Task Handle_ReturnsOnlyThatReservationsMeals_OrderedByDate()
  {
    var requested = Guid.NewGuid();
    var other = Guid.NewGuid();
    Db.Meals.Add(new Meal
    {
      ReservationId = requested,
      Date = new DateOnly(2026, 5, 3),
      Breakfast = MealAmount.Empty with { Normal = 3 },
    });
    Db.Meals.Add(new Meal
    {
      ReservationId = requested,
      Date = new DateOnly(2026, 5, 1),
      Breakfast = MealAmount.Empty with { Normal = 1 },
    });
    Db.Meals.Add(new Meal
    {
      ReservationId = requested,
      Date = new DateOnly(2026, 5, 2),
      Breakfast = MealAmount.Empty with { Normal = 2 },
    });
    Db.Meals.Add(new Meal
    {
      ReservationId = other,
      Date = new DateOnly(2026, 5, 1),
      Breakfast = MealAmount.Empty with { Normal = 9 },
    });
    await Db.SaveChangesAsync();

    Result<List<MealResponse>> result = await CreateSut().Handle(
      new GetReservationMealsQuery(requested),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Count.ShouldBe(3);
    result.Value.ShouldAllBe(m => m.ReservationId == requested);
    result.Value.Select(m => m.Date).ShouldBe(new[]
    {
      new DateOnly(2026, 5, 1),
      new DateOnly(2026, 5, 2),
      new DateOnly(2026, 5, 3),
    });
    result.Value.Select(m => m.Breakfast.Normal).ShouldBe(new uint[] { 1, 2, 3 });
  }

  [Fact]
  public async Task Handle_ReturnsEmpty_WhenNoMealsForReservation()
  {
    Result<List<MealResponse>> result = await CreateSut().Handle(
      new GetReservationMealsQuery(Guid.NewGuid()),
      CancellationToken.None);

    result.Value.ShouldBeEmpty();
  }
}
