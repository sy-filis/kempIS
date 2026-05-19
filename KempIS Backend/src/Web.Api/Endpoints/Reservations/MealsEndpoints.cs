using Application.Abstractions.Authentication;
using Application.Abstractions.Messaging;
using Application.Reservations.Meals;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reservations;

internal sealed class MealsEndpoints : IEndpoint
{
  public void MapEndpoint(IEndpointRouteBuilder app)
  {
    app.MapGet("meals", async (
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        IQueryHandler<GetMealsInRangeQuery, List<MealResponse>> handler,
        CancellationToken cancellationToken) =>
      {
        GetMealsInRangeQuery query = new(from, to);

        Result<List<MealResponse>> result = await handler.Handle(query, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
      })
      .WithTags(Tags.Reservations)
      .WithName("GetMealsInRange")
      .WithSummary("List meals in a date range")
      .WithDescription("""
        Returns every meal entry whose date falls within `[from, to]` (inclusive on both ends),
        ordered by reservation and then date. Used by the kitchen to see daily counts across all
        active reservations.

        **Errors:** `400` `from` is after `to`.
        """)
      .Produces<List<MealResponse>>(StatusCodes.Status200OK)
      .ProducesValidationProblem()
      .HasRole(Roles.Receptionist, Roles.Manager);

    app.MapGet("meals/totals", async (
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        IQueryHandler<GetMealTotalsInRangeQuery, List<MealTotalsResponse>> handler,
        CancellationToken cancellationToken) =>
      {
        GetMealTotalsInRangeQuery query = new(from, to);

        Result<List<MealTotalsResponse>> result = await handler.Handle(query, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
      })
      .WithTags(Tags.Reservations)
      .WithName("GetMealTotalsInRange")
      .WithSummary("Per-day meal totals across reservations")
      .WithDescription("""
        Returns one row per date in `[from, to]` (inclusive on both ends) that has at least
        one meal entry, each containing four meal-type buckets (breakfast, lunch,
        lunch-package, dinner) with diet-bucket counts summed across every reservation.
        Used by the kitchen to plan daily preparation.

        **Errors:** `400` `from` is after `to`.
        """)
      .Produces<List<MealTotalsResponse>>(StatusCodes.Status200OK)
      .ProducesValidationProblem()
      .HasRole(Roles.Receptionist, Roles.Manager);

    RouteGroupBuilder reservationMeals = app
      .MapGroup("reservations/{reservationId:guid}/meals")
      .WithTags(Tags.Reservations)
      .HasRole(Roles.Receptionist, Roles.Manager);

    reservationMeals.MapGet(string.Empty, async (
        Guid reservationId,
        IQueryHandler<GetReservationMealsQuery, List<MealResponse>> handler,
        CancellationToken cancellationToken) =>
      {
        GetReservationMealsQuery query = new(reservationId);

        Result<List<MealResponse>> result = await handler.Handle(query, cancellationToken);

        return result.Match(Results.Ok, CustomResults.Problem);
      })
      .WithName("GetReservationMeals")
      .WithSummary("List meals for a reservation")
      .WithDescription("""
        Returns the meal entries attached to a single reservation, ordered by date.
        """)
      .Produces<List<MealResponse>>(StatusCodes.Status200OK);

    reservationMeals.MapPost(string.Empty, async (
        Guid reservationId,
        ReplaceMealRequest request,
        ICommandHandler<ReplaceReservationMealCommand> handler,
        CancellationToken cancellationToken) =>
      {
        ReplaceReservationMealCommand command = new(
          reservationId,
          request.Date,
          request.Breakfast,
          request.Lunch,
          request.LunchPackage,
          request.Dinner);

        Result result = await handler.Handle(command, cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
      })
      .WithName("ReplaceReservationMeal")
      .WithSummary("Set the meal counts for a reservation on a date")
      .WithDescription("""
        Upserts the meal counts (breakfast, lunch, lunch-package, dinner) for the reservation on
        the supplied date. Existing entry for that date is overwritten in place; a missing
        entry is created.

        **Behavior:** the date must fall within the reservation's stay period.

        **Errors:** `400` invalid payload, or the date is outside the reservation's period.
        `404` reservation does not exist.
        """)
      .Produces(StatusCodes.Status204NoContent)
      .ProducesValidationProblem()
      .ProducesProblem(StatusCodes.Status404NotFound);

    reservationMeals.MapDelete(string.Empty, async (
        Guid reservationId,
        ICommandHandler<DeleteReservationMealsCommand> handler,
        CancellationToken cancellationToken) =>
      {
        DeleteReservationMealsCommand command = new(reservationId);

        Result result = await handler.Handle(command, cancellationToken);

        return result.Match(Results.NoContent, CustomResults.Problem);
      })
      .WithName("DeleteReservationMeals")
      .WithSummary("Delete every meal entry for a reservation")
      .WithDescription("""
        Removes all meal entries attached to the reservation in a single bulk delete. Succeeds
        silently when there are no entries.
        """)
      .Produces(StatusCodes.Status204NoContent);
  }
}

internal sealed record ReplaceMealRequest(
  DateOnly Date,
  MealAmountDto Breakfast,
  MealAmountDto Lunch,
  MealAmountDto LunchPackage,
  MealAmountDto Dinner);
