using Application.Abstractions.Authentication;
using Domain.Reservations.ReservationStates;
using TestUtilities.Builders;
using Web.Api.IntegrationTests.Infrastructure;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Web.Api.IntegrationTests.Reservations;

public sealed class GetMonthlyReservationSummaryEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
  private readonly ApiFactory _factory;

  public GetMonthlyReservationSummaryEndpointTests(ApiFactory factory) => _factory = factory;

  public Task InitializeAsync() => _factory.ResetReservationsAsync();
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

  [Fact]
  public async Task Get_NoAuth_Returns401()
  {
    HttpResponseMessage response = await Client().GetAsync(
      new Uri("reservations/monthly-summary?year=2026", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
  }

  [Fact]
  public async Task Get_WrongRole_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.CleaningStaff).GetAsync(
      new Uri("reservations/monthly-summary?year=2026", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  [Fact]
  public async Task Get_InvalidYearTooLow_Returns400()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("reservations/monthly-summary?year=1500", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Get_InvalidYearTooHigh_Returns400()
  {
    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("reservations/monthly-summary?year=2200", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  [Fact]
  public async Task Get_HappyPath_ReturnsCountsForOverlappingActiveReservations()
  {
    DomainReservation insideMarch = new ReservationBuilder()
      .WithId(Guid.NewGuid())
      .For(new DateOnly(2026, 3, 5), new DateOnly(2026, 3, 12))
      .InState(ReservationState.Confirmed)
      .Build();
    DomainReservation marchToApril = new ReservationBuilder()
      .WithId(Guid.NewGuid())
      .For(new DateOnly(2026, 3, 30), new DateOnly(2026, 4, 4))
      .InState(ReservationState.CheckedIn)
      .Build();
    DomainReservation cancelledInMay = new ReservationBuilder()
      .WithId(Guid.NewGuid())
      .For(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5))
      .InState(ReservationState.Cancelled)
      .Build();
    DomainReservation differentYear = new ReservationBuilder()
      .WithId(Guid.NewGuid())
      .For(new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 5))
      .InState(ReservationState.Confirmed)
      .Build();
    await _factory.WithDbAsync(async db =>
    {
      db.Reservations.AddRange(insideMarch, marchToApril, cancelledInMay, differentYear);
      await db.SaveChangesAsync();
    });

    HttpResponseMessage response = await Client(Roles.Receptionist).GetAsync(
      new Uri("reservations/monthly-summary?year=2026", UriKind.Relative));

    response.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      _factory.ServerExceptions.TryPeek(out Exception? ex) ? ex.ToString() : "no exception");

    MonthlyReservationSummaryDto? body =
      await response.Content.ReadFromJsonAsync<MonthlyReservationSummaryDto>();
    body.ShouldNotBeNull();
    body.Year.ShouldBe(2026);
    body.Months.Count.ShouldBe(12);
    body.Months[2].ShouldBe(2);   // March: insideMarch + marchToApril
    body.Months[3].ShouldBe(1);   // April: marchToApril
    body.Months[4].ShouldBe(0);   // May: cancelledInMay excluded by state
    body.Months.Where((_, i) => i != 2 && i != 3).ShouldAllBe(m => m == 0);
  }

  [Fact]
  public async Task Get_AsAccountant_Returns403()
  {
    HttpResponseMessage response = await Client(Roles.Accountant).GetAsync(
      new Uri("reservations/monthly-summary?year=2026", UriKind.Relative));

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
  }

  private sealed record MonthlyReservationSummaryDto(int Year, IReadOnlyList<int> Months);
}
