using Application.Reservations.Commands.CancelGroupReservation;
using Domain.Reservations;
using Domain.Reservations.GroupReservations;
using SharedKernel;

namespace Application.UnitTests.Reservations.Commands.CancelGroupReservation;

public sealed class CancelGroupReservationCommandHandlerTests : HandlerTestBase
{
  private CancelGroupReservationCommandHandler CreateSut() => new(Db, Clock);

  private async Task<GroupReservation> SeedGroup(GroupReservationState state = GroupReservationState.Confirmed)
  {
    GroupReservation g = new GroupReservationBuilder().InState(state).Build();
    Db.GroupReservations.Add(g);
    await Db.SaveChangesAsync();
    return g;
  }

  [Fact]
  public async Task Handle_FromConfirmed_TransitionsToCanceled()
  {
    GroupReservation g = await SeedGroup();
    var cancelAt = new DateTime(2026, 5, 30, 10, 0, 0, DateTimeKind.Utc);
    Clock.Set(cancelAt);

    Result result = await CreateSut().Handle(
        new CancelGroupReservationCommand(g.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    GroupReservation reloaded = await Db.GroupReservations.AsNoTracking().SingleAsync(x => x.Id == g.Id);
    reloaded.State.ShouldBe(GroupReservationState.Canceled);
    reloaded.UpdatedAtUtc.ShouldBe(cancelAt);
  }

  [Fact]
  public async Task Handle_GroupMissing_ReturnsNotFound()
  {
    var missing = Guid.NewGuid();

    Result result = await CreateSut().Handle(
        new CancelGroupReservationCommand(missing), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(GroupReservationErrors.NotFound(missing));
  }

  [Fact]
  public async Task Handle_AlreadyCanceled_ReturnsAlreadyCanceled_AndDoesNotUpdateTimestamp()
  {
    var existingUpdate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    GroupReservation g = new GroupReservationBuilder()
      .InState(GroupReservationState.Canceled)
      .UpdatedAt(existingUpdate)
      .Build();
    Db.GroupReservations.Add(g);
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(
        new CancelGroupReservationCommand(g.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(GroupReservationErrors.AlreadyCanceled(g.Id));
    GroupReservation reloaded = await Db.GroupReservations.AsNoTracking().SingleAsync(x => x.Id == g.Id);
    reloaded.UpdatedAtUtc.ShouldBe(existingUpdate);
  }
}
