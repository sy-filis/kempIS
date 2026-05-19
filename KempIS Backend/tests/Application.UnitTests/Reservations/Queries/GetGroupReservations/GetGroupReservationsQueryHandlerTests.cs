using Application.Reservations.Queries.GetGroupReservations;
using Domain.Reservations.GroupReservations;
using SharedKernel;

namespace Application.UnitTests.Reservations.Queries.GetGroupReservations;

public sealed class GetGroupReservationsQueryHandlerTests : HandlerTestBase
{
  private static readonly DateOnly QFrom = new(2026, 7, 10);
  private static readonly DateOnly QTo = new(2026, 7, 20);

  private GetGroupReservationsQueryHandler CreateSut() => new(Db);

  private async Task<GroupReservation> Seed(GroupReservation gr)
  {
    Db.GroupReservations.Add(gr);
    await Db.SaveChangesAsync();
    return gr;
  }

  [Fact]
  public async Task Handle_RowOverlapsRange_IsReturned()
  {
    GroupReservation inside = await Seed(new GroupReservationBuilder()
      .For(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14))
      .Build());

    Result<List<GroupReservationListItemResponse>> result = await CreateSut().Handle(new GetGroupReservationsQuery(QFrom, QTo), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.ShouldHaveSingleItem().Id.ShouldBe(inside.Id);
  }

  [Fact]
  public async Task Handle_RowEntirelyBeforeFrom_IsExcluded()
  {
    await Seed(new GroupReservationBuilder()
      .For(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 5))
      .Build());

    Result<List<GroupReservationListItemResponse>> result = await CreateSut().Handle(new GetGroupReservationsQuery(QFrom, QTo), CancellationToken.None);

    result.Value.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_RowEntirelyAfterTo_IsExcluded()
  {
    await Seed(new GroupReservationBuilder()
      .For(new DateOnly(2026, 7, 25), new DateOnly(2026, 7, 30))
      .Build());

    Result<List<GroupReservationListItemResponse>> result = await CreateSut().Handle(new GetGroupReservationsQuery(QFrom, QTo), CancellationToken.None);

    result.Value.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_RowEnvelopesRange_IsReturned()
  {
    GroupReservation envelope = await Seed(new GroupReservationBuilder()
      .For(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 30))
      .Build());

    Result<List<GroupReservationListItemResponse>> result = await CreateSut().Handle(new GetGroupReservationsQuery(QFrom, QTo), CancellationToken.None);

    result.Value.ShouldHaveSingleItem().Id.ShouldBe(envelope.Id);
  }

  [Fact]
  public async Task Handle_RowPeriodToEqualsFrom_IsReturned()
  {
    GroupReservation boundary = await Seed(new GroupReservationBuilder()
      .For(new DateOnly(2026, 7, 5), new DateOnly(2026, 7, 10))
      .Build());

    Result<List<GroupReservationListItemResponse>> result = await CreateSut().Handle(new GetGroupReservationsQuery(QFrom, QTo), CancellationToken.None);

    result.Value.ShouldHaveSingleItem().Id.ShouldBe(boundary.Id);
  }

  [Fact]
  public async Task Handle_RowPeriodFromEqualsTo_IsReturned()
  {
    GroupReservation boundary = await Seed(new GroupReservationBuilder()
      .For(new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 25))
      .Build());

    Result<List<GroupReservationListItemResponse>> result = await CreateSut().Handle(new GetGroupReservationsQuery(QFrom, QTo), CancellationToken.None);

    result.Value.ShouldHaveSingleItem().Id.ShouldBe(boundary.Id);
  }

  [Fact]
  public async Task Handle_StateFilter_OnlyReturnsRowsInThatState()
  {
    await Seed(new GroupReservationBuilder()
      .WithId(Guid.NewGuid())
      .InState(GroupReservationState.Confirmed)
      .For(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14))
      .Build());
    GroupReservation cancelled = await Seed(new GroupReservationBuilder()
      .WithId(Guid.NewGuid())
      .InState(GroupReservationState.Canceled)
      .For(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14))
      .Build());

    Result<List<GroupReservationListItemResponse>> result = await CreateSut().Handle(
      new GetGroupReservationsQuery(QFrom, QTo, GroupReservationState.Canceled),
      CancellationToken.None);

    result.Value.ShouldHaveSingleItem().Id.ShouldBe(cancelled.Id);
  }

  [Fact]
  public async Task Handle_NoStateFilter_ReturnsBothConfirmedAndCanceled()
  {
    await Seed(new GroupReservationBuilder()
      .WithId(Guid.NewGuid())
      .InState(GroupReservationState.Confirmed)
      .For(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14))
      .Build());
    await Seed(new GroupReservationBuilder()
      .WithId(Guid.NewGuid())
      .InState(GroupReservationState.Canceled)
      .For(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14))
      .Build());

    Result<List<GroupReservationListItemResponse>> result = await CreateSut().Handle(new GetGroupReservationsQuery(QFrom, QTo), CancellationToken.None);

    result.Value.Count.ShouldBe(2);
  }

  [Fact]
  public async Task Handle_ProjectsOrganizerPhoneAndSpotIds()
  {
    var s1 = Guid.NewGuid();
    var s2 = Guid.NewGuid();
    await Seed(new GroupReservationBuilder()
      .For(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14))
      .WithOrganizer("Alice", "alice@example.com")
      .WithOrganizerPhone("+420 999 111 222")
      .HoldingSpots(s1, s2)
      .Build());

    Result<List<GroupReservationListItemResponse>> result = await CreateSut().Handle(new GetGroupReservationsQuery(QFrom, QTo), CancellationToken.None);

    GroupReservationListItemResponse item = result.Value.ShouldHaveSingleItem();
    item.OrganizerPhone.ShouldBe("+420 999 111 222");
    item.SpotIds.ShouldBe(new[] { s1, s2 }, ignoreOrder: true);
  }

  [Fact]
  public async Task Handle_OrdersByPeriodFromAscendingThenCreatedAtUtcAscending()
  {
    GroupReservation later = await Seed(new GroupReservationBuilder()
      .WithId(Guid.NewGuid())
      .For(new DateOnly(2026, 7, 18), new DateOnly(2026, 7, 19))
      .CreatedAt(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc))
      .Build());
    GroupReservation earlierA = await Seed(new GroupReservationBuilder()
      .WithId(Guid.NewGuid())
      .For(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14))
      .CreatedAt(new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc))
      .Build());
    GroupReservation earlierB = await Seed(new GroupReservationBuilder()
      .WithId(Guid.NewGuid())
      .For(new DateOnly(2026, 7, 12), new DateOnly(2026, 7, 14))
      .CreatedAt(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc))
      .Build());

    Result<List<GroupReservationListItemResponse>> result = await CreateSut().Handle(new GetGroupReservationsQuery(QFrom, QTo), CancellationToken.None);

    result.Value.Select(r => r.Id).ShouldBe(new[] { earlierB.Id, earlierA.Id, later.Id });
  }
}
