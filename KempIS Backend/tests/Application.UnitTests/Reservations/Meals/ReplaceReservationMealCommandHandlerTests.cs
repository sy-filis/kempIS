using Application.Reservations.Meals;
using Domain.Reservations.Meals;
using SharedKernel;

namespace Application.UnitTests.Reservations.Meals;

public sealed class ReplaceReservationMealCommandHandlerTests : HandlerTestBase
{
  private ReplaceReservationMealCommandHandler CreateSut() => new(Db);

  private async Task<Guid> SeedReservationAsync(DateOnly from, DateOnly to)
  {
    var id = Guid.NewGuid();
    Db.Reservations.Add(new ReservationBuilder().WithId(id).For(from, to).Build());
    await Db.SaveChangesAsync();
    return id;
  }

  private static MealAmountDto Amount(uint normal = 0, uint glutenFree = 0, TimeOnly? at = null) =>
    new(at, normal, glutenFree, 0, 0, 0, 0, 0, 0);

  [Fact]
  public async Task Handle_InsertsMeal_WhenNoneExistsForDate()
  {
    Guid reservationId = await SeedReservationAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5));
    var date = new DateOnly(2026, 5, 3);

    Result result = await CreateSut().Handle(
      new ReplaceReservationMealCommand(
        reservationId,
        date,
        Amount(normal: 3, at: new TimeOnly(8, 0)),
        Amount(normal: 2, glutenFree: 1),
        Amount(),
        Amount(normal: 4)),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    Meal meal = await Db.Meals.SingleAsync(m => m.ReservationId == reservationId && m.Date == date);
    meal.Breakfast.At.ShouldBe(new TimeOnly(8, 0));
    meal.Breakfast.Normal.ShouldBe(3u);
    meal.Lunch.Normal.ShouldBe(2u);
    meal.Lunch.GlutenFree.ShouldBe(1u);
    meal.LunchPackage.Total.ShouldBe(0u);
    meal.Dinner.Normal.ShouldBe(4u);
  }

  [Fact]
  public async Task Handle_ReplacesExistingMeal_WhenRowForDateAlreadyExists()
  {
    Guid reservationId = await SeedReservationAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5));
    var date = new DateOnly(2026, 5, 3);

    await CreateSut().Handle(
      new ReplaceReservationMealCommand(reservationId, date, Amount(1), Amount(1), Amount(1), Amount(1)),
      CancellationToken.None);

    Result result = await CreateSut().Handle(
      new ReplaceReservationMealCommand(reservationId, date, Amount(9), Amount(), Amount(), Amount()),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();

    (await Db.Meals.CountAsync(m => m.ReservationId == reservationId && m.Date == date))
      .ShouldBe(1);
    Meal meal = await Db.Meals.SingleAsync(m => m.ReservationId == reservationId && m.Date == date);
    meal.Breakfast.Normal.ShouldBe(9u);
    meal.Lunch.Total.ShouldBe(0u);
  }

  [Fact]
  public async Task Handle_ReturnsReservationNotFound_WhenReservationDoesNotExist()
  {
    Result result = await CreateSut().Handle(
      new ReplaceReservationMealCommand(
        Guid.NewGuid(), new DateOnly(2026, 5, 3), Amount(1), Amount(), Amount(), Amount()),
      CancellationToken.None);

    result.IsSuccess.ShouldBeFalse();
    result.Error.Code.ShouldBe("Reservation.NotFound");
  }

  [Fact]
  public async Task Handle_ReturnsDateOutsidePeriod_WhenDateAfterReservationPeriod()
  {
    Guid reservationId = await SeedReservationAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5));

    Result result = await CreateSut().Handle(
      new ReplaceReservationMealCommand(
        reservationId, new DateOnly(2026, 6, 1), Amount(1), Amount(), Amount(), Amount()),
      CancellationToken.None);

    result.IsSuccess.ShouldBeFalse();
    result.Error.Code.ShouldBe("Meals.DateOutsideReservationPeriod");
  }

  [Fact]
  public async Task Handle_ReturnsDateOutsidePeriod_WhenDateBeforeReservationPeriod()
  {
    Guid reservationId = await SeedReservationAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5));

    Result result = await CreateSut().Handle(
      new ReplaceReservationMealCommand(
        reservationId, new DateOnly(2026, 4, 30), Amount(1), Amount(), Amount(), Amount()),
      CancellationToken.None);

    result.IsSuccess.ShouldBeFalse();
    result.Error.Code.ShouldBe("Meals.DateOutsideReservationPeriod");
  }

  [Fact]
  public async Task Handle_AllowsDateOnPeriodBoundaries()
  {
    Guid reservationId = await SeedReservationAsync(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5));

    Result fromResult = await CreateSut().Handle(
      new ReplaceReservationMealCommand(
        reservationId, new DateOnly(2026, 5, 1), Amount(1), Amount(), Amount(), Amount()),
      CancellationToken.None);
    Result toResult = await CreateSut().Handle(
      new ReplaceReservationMealCommand(
        reservationId, new DateOnly(2026, 5, 5), Amount(2), Amount(), Amount(), Amount()),
      CancellationToken.None);

    fromResult.IsSuccess.ShouldBeTrue();
    toResult.IsSuccess.ShouldBeTrue();
  }
}
