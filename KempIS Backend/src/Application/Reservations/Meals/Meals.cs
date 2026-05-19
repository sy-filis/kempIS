using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Reservations;
using Domain.Reservations.Meals;
using Domain.Reservations.Reservations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reservations.Meals;

public sealed record MealAmountDto(
  TimeOnly? At,
  uint Normal,
  uint GlutenFree,
  uint LactoseFree,
  uint Vegetarian,
  uint GlutenFreeLactoseFree,
  uint GlutenFreeVegetarian,
  uint LactoseFreeVegetarian,
  uint GlutenFreeLactoseFreeVegetarian);

internal static class MealAmountMapping
{
  public static MealAmountDto ToDto(this MealAmount source) =>
    new(source.At,
        source.Normal,
        source.GlutenFree,
        source.LactoseFree,
        source.Vegetarian,
        source.GlutenFreeLactoseFree,
        source.GlutenFreeVegetarian,
        source.LactoseFreeVegetarian,
        source.GlutenFreeLactoseFreeVegetarian);

  public static MealAmount ToDomain(this MealAmountDto source) =>
    new()
    {
      At = source.At,
      Normal = source.Normal,
      GlutenFree = source.GlutenFree,
      LactoseFree = source.LactoseFree,
      Vegetarian = source.Vegetarian,
      GlutenFreeLactoseFree = source.GlutenFreeLactoseFree,
      GlutenFreeVegetarian = source.GlutenFreeVegetarian,
      LactoseFreeVegetarian = source.LactoseFreeVegetarian,
      GlutenFreeLactoseFreeVegetarian = source.GlutenFreeLactoseFreeVegetarian,
    };
}

public sealed record MealResponse(
  Guid ReservationId,
  DateOnly Date,
  MealAmountDto Breakfast,
  MealAmountDto Lunch,
  MealAmountDto LunchPackage,
  MealAmountDto Dinner);

public sealed record GetMealsInRangeQuery(DateOnly From, DateOnly To)
  : IQuery<List<MealResponse>>;

internal sealed class GetMealsInRangeQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetMealsInRangeQuery, List<MealResponse>>
{
  public async Task<Result<List<MealResponse>>> Handle(
    GetMealsInRangeQuery query,
    CancellationToken cancellationToken)
  {
    List<Meal> meals = await context.Meals
      .AsNoTracking()
      .Where(m => m.Date >= query.From && m.Date <= query.To)
      .OrderBy(m => m.ReservationId)
      .ThenBy(m => m.Date)
      .ToListAsync(cancellationToken);

    return meals.ConvertAll(m => new MealResponse(
      m.ReservationId,
      m.Date,
      m.Breakfast.ToDto(),
      m.Lunch.ToDto(),
      m.LunchPackage.ToDto(),
      m.Dinner.ToDto()));
  }
}

internal sealed class GetMealsInRangeQueryValidator : AbstractValidator<GetMealsInRangeQuery>
{
  public GetMealsInRangeQueryValidator()
  {
    RuleFor(q => q.From)
      .LessThanOrEqualTo(q => q.To)
      .WithMessage("'From' must be on or before 'To'.");
  }
}

public sealed record GetReservationMealsQuery(Guid ReservationId)
  : IQuery<List<MealResponse>>;

internal sealed class GetReservationMealsQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetReservationMealsQuery, List<MealResponse>>
{
  public async Task<Result<List<MealResponse>>> Handle(
    GetReservationMealsQuery query,
    CancellationToken cancellationToken)
  {
    List<Meal> meals = await context.Meals
      .AsNoTracking()
      .Where(m => m.ReservationId == query.ReservationId)
      .OrderBy(m => m.Date)
      .ToListAsync(cancellationToken);

    return meals.ConvertAll(m => new MealResponse(
      m.ReservationId,
      m.Date,
      m.Breakfast.ToDto(),
      m.Lunch.ToDto(),
      m.LunchPackage.ToDto(),
      m.Dinner.ToDto()));
  }
}

internal sealed class GetReservationMealsQueryValidator : AbstractValidator<GetReservationMealsQuery>
{
  public GetReservationMealsQueryValidator()
  {
    RuleFor(q => q.ReservationId)
      .NotEmpty();
  }
}

public sealed record ReplaceReservationMealCommand(
  Guid ReservationId,
  DateOnly Date,
  MealAmountDto Breakfast,
  MealAmountDto Lunch,
  MealAmountDto LunchPackage,
  MealAmountDto Dinner) : ICommand;

internal sealed class ReplaceReservationMealCommandHandler(IApplicationDbContext context)
  : ICommandHandler<ReplaceReservationMealCommand>
{
  public async Task<Result> Handle(
    ReplaceReservationMealCommand command,
    CancellationToken cancellationToken)
  {
    Reservation? reservation = await context.Reservations
      .FirstOrDefaultAsync(r => r.Id == command.ReservationId, cancellationToken);

    if (reservation is null)
    {
      return Result.Failure(ReservationErrors.NotFound(command.ReservationId));
    }

    if (!reservation.Period.Contains(command.Date))
    {
      return Result.Failure(MealErrors.DateOutsideReservationPeriod(command.Date, command.ReservationId));
    }

    Meal? existing = await context.Meals
      .FirstOrDefaultAsync(
        m => m.ReservationId == command.ReservationId && m.Date == command.Date,
        cancellationToken);

    MealAmount breakfast = command.Breakfast.ToDomain();
    MealAmount lunch = command.Lunch.ToDomain();
    MealAmount lunchPackage = command.LunchPackage.ToDomain();
    MealAmount dinner = command.Dinner.ToDomain();

    if (existing is null)
    {
      Meal meal = new()
      {
        ReservationId = command.ReservationId,
        Date = command.Date,
        Breakfast = breakfast,
        Lunch = lunch,
        LunchPackage = lunchPackage,
        Dinner = dinner,
      };
      context.Meals.Add(meal);
    }
    else
    {
      existing.Breakfast = breakfast;
      existing.Lunch = lunch;
      existing.LunchPackage = lunchPackage;
      existing.Dinner = dinner;
    }

    await context.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class ReplaceReservationMealCommandValidator : AbstractValidator<ReplaceReservationMealCommand>
{
  public ReplaceReservationMealCommandValidator()
  {
    RuleFor(c => c.ReservationId)
      .NotEmpty();

    RuleFor(c => c.Date)
      .NotEmpty();

    RuleFor(c => c.Breakfast).NotNull();
    RuleFor(c => c.Lunch).NotNull();
    RuleFor(c => c.LunchPackage).NotNull();
    RuleFor(c => c.Dinner).NotNull();
  }
}

public sealed record DeleteReservationMealsCommand(Guid ReservationId) : ICommand;

internal sealed class DeleteReservationMealsCommandHandler(IApplicationDbContext context)
  : ICommandHandler<DeleteReservationMealsCommand>
{
  public async Task<Result> Handle(
    DeleteReservationMealsCommand command,
    CancellationToken cancellationToken)
  {
    await context.Meals
      .Where(m => m.ReservationId == command.ReservationId)
      .ExecuteDeleteAsync(cancellationToken);

    return Result.Success();
  }
}

internal sealed class DeleteReservationMealsCommandValidator : AbstractValidator<DeleteReservationMealsCommand>
{
  public DeleteReservationMealsCommandValidator()
  {
    RuleFor(c => c.ReservationId)
      .NotEmpty();
  }
}

public sealed record MealTotalsAmountDto(
  uint Normal,
  uint GlutenFree,
  uint LactoseFree,
  uint Vegetarian,
  uint GlutenFreeLactoseFree,
  uint GlutenFreeVegetarian,
  uint LactoseFreeVegetarian,
  uint GlutenFreeLactoseFreeVegetarian);

public sealed record MealTotalsResponse(
  DateOnly Date,
  MealTotalsAmountDto Breakfast,
  MealTotalsAmountDto Lunch,
  MealTotalsAmountDto LunchPackage,
  MealTotalsAmountDto Dinner);

public sealed record GetMealTotalsInRangeQuery(DateOnly From, DateOnly To)
  : IQuery<List<MealTotalsResponse>>;

internal sealed class GetMealTotalsInRangeQueryHandler(IApplicationDbContext context)
  : IQueryHandler<GetMealTotalsInRangeQuery, List<MealTotalsResponse>>
{
  public async Task<Result<List<MealTotalsResponse>>> Handle(
    GetMealTotalsInRangeQuery query,
    CancellationToken cancellationToken)
  {
    List<Meal> meals = await context.Meals
      .AsNoTracking()
      .Where(m => m.Date >= query.From && m.Date <= query.To)
      .ToListAsync(cancellationToken);

    return meals
      .GroupBy(m => m.Date)
      .OrderBy(g => g.Key)
      .Select(g => new MealTotalsResponse(
        g.Key,
        SumBuckets(g, m => m.Breakfast),
        SumBuckets(g, m => m.Lunch),
        SumBuckets(g, m => m.LunchPackage),
        SumBuckets(g, m => m.Dinner)))
      .ToList();
  }

  private static MealTotalsAmountDto SumBuckets(
    IEnumerable<Meal> meals,
    Func<Meal, MealAmount> selector)
  {
    uint normal = 0u;
    uint glutenFree = 0u;
    uint lactoseFree = 0u;
    uint vegetarian = 0u;
    uint glutenFreeLactoseFree = 0u;
    uint glutenFreeVegetarian = 0u;
    uint lactoseFreeVegetarian = 0u;
    uint glutenFreeLactoseFreeVegetarian = 0u;

    foreach (Meal meal in meals)
    {
      MealAmount amount = selector(meal);
      normal += amount.Normal;
      glutenFree += amount.GlutenFree;
      lactoseFree += amount.LactoseFree;
      vegetarian += amount.Vegetarian;
      glutenFreeLactoseFree += amount.GlutenFreeLactoseFree;
      glutenFreeVegetarian += amount.GlutenFreeVegetarian;
      lactoseFreeVegetarian += amount.LactoseFreeVegetarian;
      glutenFreeLactoseFreeVegetarian += amount.GlutenFreeLactoseFreeVegetarian;
    }

    return new MealTotalsAmountDto(
      normal,
      glutenFree,
      lactoseFree,
      vegetarian,
      glutenFreeLactoseFree,
      glutenFreeVegetarian,
      lactoseFreeVegetarian,
      glutenFreeLactoseFreeVegetarian);
  }
}

internal sealed class GetMealTotalsInRangeQueryValidator : AbstractValidator<GetMealTotalsInRangeQuery>
{
  public GetMealTotalsInRangeQueryValidator()
  {
    RuleFor(q => q.From)
      .LessThanOrEqualTo(q => q.To)
      .WithMessage("'From' must be on or before 'To'.");
  }
}
