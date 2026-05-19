using Application.Reservations.Meals;
using Domain.Reservations.Meals;
using SharedKernel;

namespace Application.UnitTests.Reservations.Meals;

public sealed class DeleteReservationMealsCommandHandlerTests : HandlerTestBase
{
  private DeleteReservationMealsCommandHandler CreateSut() => new(Db);

  [Fact]
  public async Task Handle_DeletesOnlyThatReservationsMeals()
  {
    var keepId = Guid.NewGuid();
    var deleteId = Guid.NewGuid();
    var date = new DateOnly(2026, 5, 2);
    Db.Meals.Add(new Meal
    {
      ReservationId = keepId,
      Date = date,
      Breakfast = MealAmount.Empty with { Normal = 1 },
    });
    Db.Meals.Add(new Meal
    {
      ReservationId = deleteId,
      Date = date,
      Breakfast = MealAmount.Empty with { Normal = 2 },
    });
    Db.Meals.Add(new Meal
    {
      ReservationId = deleteId,
      Date = date.AddDays(1),
      Breakfast = MealAmount.Empty with { Normal = 3 },
    });
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(
      new DeleteReservationMealsCommand(deleteId),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    (await Db.Meals.CountAsync(m => m.ReservationId == deleteId)).ShouldBe(0);
    (await Db.Meals.CountAsync(m => m.ReservationId == keepId)).ShouldBe(1);
  }

  [Fact]
  public async Task Handle_IsNoOp_WhenNoMealsExistForReservation()
  {
    Result result = await CreateSut().Handle(
      new DeleteReservationMealsCommand(Guid.NewGuid()),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
  }
}
