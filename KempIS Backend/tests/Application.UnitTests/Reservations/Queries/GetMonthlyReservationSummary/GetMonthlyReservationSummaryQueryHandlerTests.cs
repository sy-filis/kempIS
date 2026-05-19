using Application.Reservations.Queries.GetMonthlyReservationSummary;
using Domain.Reservations.ReservationStates;
using SharedKernel;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Application.UnitTests.Reservations.Queries.GetMonthlyReservationSummary;

public sealed class GetMonthlyReservationSummaryQueryHandlerTests : HandlerTestBase
{
  private const int Year = 2026;

  private GetMonthlyReservationSummaryQueryHandler CreateSut() => new(Db);

  private async Task Seed(params DomainReservation[] reservations)
  {
    Db.Reservations.AddRange(reservations);
    await Db.SaveChangesAsync();
  }

  [Fact]
  public async Task Handle_NoReservations_ReturnsTwelveZeros()
  {
    Result<MonthlyReservationSummaryResponse> result =
      await CreateSut().Handle(new GetMonthlyReservationSummaryQuery(Year), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Year.ShouldBe(Year);
    result.Value.Months.Count.ShouldBe(12);
    result.Value.Months.ShouldAllBe(m => m == 0);
  }

  [Fact]
  public async Task Handle_SingleReservationInsideOneMonth_IncrementsThatBucket()
  {
    await Seed(new ReservationBuilder()
      .For(new DateOnly(Year, 3, 5), new DateOnly(Year, 3, 12))
      .InState(ReservationState.Confirmed)
      .Build());

    Result<MonthlyReservationSummaryResponse> result =
      await CreateSut().Handle(new GetMonthlyReservationSummaryQuery(Year), CancellationToken.None);

    result.Value.Months[2].ShouldBe(1);
    result.Value.Months.Where((_, i) => i != 2).ShouldAllBe(m => m == 0);
  }

  [Fact]
  public async Task Handle_ReservationSpanningTwoMonths_IncrementsBothBuckets()
  {
    await Seed(new ReservationBuilder()
      .For(new DateOnly(Year, 3, 30), new DateOnly(Year, 4, 4))
      .InState(ReservationState.Confirmed)
      .Build());

    Result<MonthlyReservationSummaryResponse> result =
      await CreateSut().Handle(new GetMonthlyReservationSummaryQuery(Year), CancellationToken.None);

    result.Value.Months[2].ShouldBe(1);
    result.Value.Months[3].ShouldBe(1);
    result.Value.Months.Where((_, i) => i != 2 && i != 3).ShouldAllBe(m => m == 0);
  }

  [Fact]
  public async Task Handle_ReservationCrossingPriorYearBoundary_OnlyJanuaryIncrements()
  {
    await Seed(new ReservationBuilder()
      .For(new DateOnly(Year - 1, 12, 29), new DateOnly(Year, 1, 3))
      .InState(ReservationState.Confirmed)
      .Build());

    Result<MonthlyReservationSummaryResponse> result =
      await CreateSut().Handle(new GetMonthlyReservationSummaryQuery(Year), CancellationToken.None);

    result.Value.Months[0].ShouldBe(1);
    result.Value.Months.Where((_, i) => i != 0).ShouldAllBe(m => m == 0);
  }

  [Fact]
  public async Task Handle_ReservationCrossingNextYearBoundary_OnlyDecemberIncrements()
  {
    await Seed(new ReservationBuilder()
      .For(new DateOnly(Year, 12, 28), new DateOnly(Year + 1, 1, 4))
      .InState(ReservationState.Confirmed)
      .Build());

    Result<MonthlyReservationSummaryResponse> result =
      await CreateSut().Handle(new GetMonthlyReservationSummaryQuery(Year), CancellationToken.None);

    result.Value.Months[11].ShouldBe(1);
    result.Value.Months.Where((_, i) => i != 11).ShouldAllBe(m => m == 0);
  }

  [Fact]
  public async Task Handle_ReservationEntirelyOutsideRequestedYear_IsExcluded()
  {
    await Seed(new ReservationBuilder()
      .For(new DateOnly(Year - 1, 6, 1), new DateOnly(Year - 1, 6, 5))
      .InState(ReservationState.Confirmed)
      .Build());

    Result<MonthlyReservationSummaryResponse> result =
      await CreateSut().Handle(new GetMonthlyReservationSummaryQuery(Year), CancellationToken.None);

    result.Value.Months.ShouldAllBe(m => m == 0);
  }

  [Fact]
  public async Task Handle_CreatedAndCancelledStates_AreExcluded()
  {
    await Seed(
      new ReservationBuilder()
        .WithId(Guid.NewGuid())
        .For(new DateOnly(Year, 5, 1), new DateOnly(Year, 5, 5))
        .InState(ReservationState.Created)
        .Build(),
      new ReservationBuilder()
        .WithId(Guid.NewGuid())
        .For(new DateOnly(Year, 5, 1), new DateOnly(Year, 5, 5))
        .InState(ReservationState.Cancelled)
        .Build());

    Result<MonthlyReservationSummaryResponse> result =
      await CreateSut().Handle(new GetMonthlyReservationSummaryQuery(Year), CancellationToken.None);

    result.Value.Months.ShouldAllBe(m => m == 0);
  }

  [Fact]
  public async Task Handle_AllActiveStates_AreCounted()
  {
    await Seed(
      new ReservationBuilder()
        .WithId(Guid.NewGuid())
        .For(new DateOnly(Year, 6, 1), new DateOnly(Year, 6, 5))
        .InState(ReservationState.Confirmed)
        .Build(),
      new ReservationBuilder()
        .WithId(Guid.NewGuid())
        .For(new DateOnly(Year, 6, 10), new DateOnly(Year, 6, 12))
        .InState(ReservationState.CheckedIn)
        .Build(),
      new ReservationBuilder()
        .WithId(Guid.NewGuid())
        .For(new DateOnly(Year, 6, 20), new DateOnly(Year, 6, 25))
        .InState(ReservationState.Completed)
        .Build());

    Result<MonthlyReservationSummaryResponse> result =
      await CreateSut().Handle(new GetMonthlyReservationSummaryQuery(Year), CancellationToken.None);

    result.Value.Months[5].ShouldBe(3);
    result.Value.Months.Where((_, i) => i != 5).ShouldAllBe(m => m == 0);
  }

  [Fact]
  public async Task Handle_MultipleReservationsInSameMonth_CountsAdd()
  {
    await Seed(
      new ReservationBuilder()
        .WithId(Guid.NewGuid())
        .For(new DateOnly(Year, 5, 1), new DateOnly(Year, 5, 3))
        .InState(ReservationState.Confirmed)
        .Build(),
      new ReservationBuilder()
        .WithId(Guid.NewGuid())
        .For(new DateOnly(Year, 5, 4), new DateOnly(Year, 5, 8))
        .InState(ReservationState.Confirmed)
        .Build(),
      new ReservationBuilder()
        .WithId(Guid.NewGuid())
        .For(new DateOnly(Year, 5, 20), new DateOnly(Year, 5, 25))
        .InState(ReservationState.CheckedIn)
        .Build());

    Result<MonthlyReservationSummaryResponse> result =
      await CreateSut().Handle(new GetMonthlyReservationSummaryQuery(Year), CancellationToken.None);

    result.Value.Months[4].ShouldBe(3);
  }

  [Fact]
  public async Task Handle_ReservationSpanningWholeYear_IncrementsEveryMonth()
  {
    await Seed(new ReservationBuilder()
      .For(new DateOnly(Year - 1, 11, 1), new DateOnly(Year + 1, 2, 28))
      .InState(ReservationState.Confirmed)
      .Build());

    Result<MonthlyReservationSummaryResponse> result =
      await CreateSut().Handle(new GetMonthlyReservationSummaryQuery(Year), CancellationToken.None);

    result.Value.Months.ShouldAllBe(m => m == 1);
  }
}
