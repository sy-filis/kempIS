using Application.Abstractions.Authentication;
using Application.Reservations.Meals;
using Domain.Reservations.Meals;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Web.Api.IntegrationTests.Reservations;

public sealed class MealsEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public MealsEndpointTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync() => _factory.ResetAllAsync();
  public Task DisposeAsync() => Task.CompletedTask;

  private HttpClient Client(params string[] roles)
  {
    HttpClient client = _factory.CreateClient();
    if (roles.Length > 0)
    {
      client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(",", roles));
    }
    return client;
  }

  private static readonly DateOnly PeriodFrom = new(2026, 6, 1);
  private static readonly DateOnly PeriodTo = new(2026, 6, 5);

  private async Task<Guid> SeedReservationAsync()
  {
    var reservationId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(new ReservationBuilder()
        .WithId(reservationId)
        .For(PeriodFrom, PeriodTo)
        .Build());
      await db.SaveChangesAsync();
    });
    return reservationId;
  }

  private static object MealAmountBody(uint normal = 0) => new
  {
    At = (TimeOnly?)null,
    Normal = normal,
    GlutenFree = 0u,
    LactoseFree = 0u,
    Vegetarian = 0u,
    GlutenFreeLactoseFree = 0u,
    GlutenFreeVegetarian = 0u,
    LactoseFreeVegetarian = 0u,
    GlutenFreeLactoseFreeVegetarian = 0u,
  };

  private static object UpsertBody(DateOnly date, uint breakfast = 2, uint lunch = 0, uint lunchPackage = 0, uint dinner = 0) => new
  {
    Date = date,
    Breakfast = MealAmountBody(breakfast),
    Lunch = MealAmountBody(lunch),
    LunchPackage = MealAmountBody(lunchPackage),
    Dinner = MealAmountBody(dinner),
  };

  [Fact]
  public async Task Crud_Manager_Roundtrip_Succeeds()
  {
    HttpClient client = Client(Roles.Manager);
    Guid reservationId = await SeedReservationAsync();
    var date = new DateOnly(2026, 6, 2);

    HttpResponseMessage postResponse = await client.PostAsJsonAsync(
      new Uri($"reservations/{reservationId}/meals", UriKind.Relative),
      UpsertBody(date, breakfast: 3, lunch: 2, dinner: 4));
    postResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent,
      _factory.ServerExceptions.TryPeek(out Exception? ex1) ? ex1.ToString() : "no exception");

    HttpResponseMessage rangeResponse = await client.GetAsync(
      new Uri($"meals?from={PeriodFrom:yyyy-MM-dd}&to={PeriodTo:yyyy-MM-dd}", UriKind.Relative));
    rangeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    string rangeBody = await rangeResponse.Content.ReadAsStringAsync();
    rangeBody.ShouldContain(reservationId.ToString());
    rangeBody.ShouldContain("\"normal\":3");

    HttpResponseMessage perReservationResponse = await client.GetAsync(
      new Uri($"reservations/{reservationId}/meals", UriKind.Relative));
    perReservationResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await perReservationResponse.Content.ReadAsStringAsync()).ShouldContain("\"normal\":3");

    // Replace semantics: same date, new amounts - row is updated, not duplicated.
    HttpResponseMessage replaceResponse = await client.PostAsJsonAsync(
      new Uri($"reservations/{reservationId}/meals", UriKind.Relative),
      UpsertBody(date, breakfast: 9));
    replaceResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    HttpResponseMessage afterReplaceResponse = await client.GetAsync(
      new Uri($"reservations/{reservationId}/meals", UriKind.Relative));
    string afterReplaceBody = await afterReplaceResponse.Content.ReadAsStringAsync();
    afterReplaceBody.ShouldContain("\"normal\":9");
    afterReplaceBody.ShouldNotContain("\"normal\":3");

    HttpResponseMessage deleteResponse = await client.DeleteAsync(
      new Uri($"reservations/{reservationId}/meals", UriKind.Relative));
    deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    HttpResponseMessage afterDeleteResponse = await client.GetAsync(
      new Uri($"reservations/{reservationId}/meals", UriKind.Relative));
    (await afterDeleteResponse.Content.ReadAsStringAsync()).ShouldBe("[]");
  }

  [Fact]
  public async Task PostMeal_DateOutsidePeriod_Returns400()
  {
    HttpClient client = Client(Roles.Receptionist);
    Guid reservationId = await SeedReservationAsync();

    HttpResponseMessage response = await client.PostAsJsonAsync(
      new Uri($"reservations/{reservationId}/meals", UriKind.Relative),
      UpsertBody(new DateOnly(2026, 7, 15)));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await response.Content.ReadAsStringAsync()).ShouldContain("Meals.DateOutsideReservationPeriod");
  }

  [Fact]
  public async Task PostMeal_ReservationNotFound_Returns404()
  {
    HttpClient client = Client(Roles.Receptionist);

    HttpResponseMessage response = await client.PostAsJsonAsync(
      new Uri($"reservations/{Guid.NewGuid()}/meals", UriKind.Relative),
      UpsertBody(new DateOnly(2026, 6, 2)));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task GetMealsInRange_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(
      new Uri("meals?from=2026-06-01&to=2026-06-07", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GetMealsInRange_WrongRole_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.CleaningStaff).GetAsync(
      new Uri("meals?from=2026-06-01&to=2026-06-07", UriKind.Relative));
    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task DeleteReservationMeals_RemovesOnlyThatReservationsMeals()
  {
    HttpClient client = Client(Roles.Manager);
    Guid keepId = await SeedReservationAsync();
    var deleteId = Guid.NewGuid();
    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(new ReservationBuilder()
        .WithId(deleteId)
        .For(PeriodFrom, PeriodTo)
        .Build());
      await db.SaveChangesAsync();
    });

    await client.PostAsJsonAsync(
      new Uri($"reservations/{keepId}/meals", UriKind.Relative), UpsertBody(new DateOnly(2026, 6, 2), breakfast: 5));
    await client.PostAsJsonAsync(
      new Uri($"reservations/{deleteId}/meals", UriKind.Relative), UpsertBody(new DateOnly(2026, 6, 2), breakfast: 7));

    HttpResponseMessage deleteResponse = await client.DeleteAsync(
      new Uri($"reservations/{deleteId}/meals", UriKind.Relative));
    deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    HttpResponseMessage keepGet = await client.GetAsync(
      new Uri($"reservations/{keepId}/meals", UriKind.Relative));
    (await keepGet.Content.ReadAsStringAsync()).ShouldContain("\"normal\":5");

    HttpResponseMessage deletedGet = await client.GetAsync(
      new Uri($"reservations/{deleteId}/meals", UriKind.Relative));
    (await deletedGet.Content.ReadAsStringAsync()).ShouldBe("[]");
  }

  [Fact]
  public async Task GetTotals_Anonymous_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(
      new Uri("meals/totals?from=2026-05-01&to=2026-05-05", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task GetTotals_AsCleaningStaff_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.CleaningStaff).GetAsync(
      new Uri("meals/totals?from=2026-05-01&to=2026-05-05", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task GetTotals_FromAfterTo_Returns400()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("meals/totals?from=2026-05-05&to=2026-05-01", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task GetTotals_AggregatesAcrossReservations_GroupedByDate()
  {
    DomainReservation r1 = new ReservationBuilder()
      .For(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3))
      .Build();
    DomainReservation r2 = new ReservationBuilder()
      .For(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 3))
      .Build();

    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.Add(r1);
      db.Reservations.Add(r2);
      db.Meals.Add(new Meal
      {
        ReservationId = r1.Id,
        Date = new DateOnly(2026, 5, 2),
        Breakfast = MealAmount.Empty with { Normal = 3 },
      });
      db.Meals.Add(new Meal
      {
        ReservationId = r2.Id,
        Date = new DateOnly(2026, 5, 2),
        Breakfast = MealAmount.Empty with { Normal = 5, GlutenFree = 1 },
      });
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("meals/totals?from=2026-05-01&to=2026-05-05", UriKind.Relative));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    List<MealTotalsResponse>? body = await response.Content.ReadFromJsonAsync<List<MealTotalsResponse>>();
    body.ShouldNotBeNull();
    MealTotalsResponse row = body.ShouldHaveSingleItem();
    row.Date.ShouldBe(new DateOnly(2026, 5, 2));
    row.Breakfast.Normal.ShouldBe(8u);
    row.Breakfast.GlutenFree.ShouldBe(1u);
  }
}
