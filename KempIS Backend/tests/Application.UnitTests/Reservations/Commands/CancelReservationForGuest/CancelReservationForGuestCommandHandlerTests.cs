using Application.Reservations.Commands.CancelReservationForGuest;
using Domain.Reservations;
using Domain.Reservations.ReservationStates;
using SharedKernel;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Application.UnitTests.Reservations.Commands.CancelReservationForGuest;

public sealed class CancelReservationForGuestCommandHandlerTests : HandlerTestBase
{
  private CancelReservationForGuestCommandHandler CreateSut() => new(Db, Clock);

  private async Task<DomainReservation> SeedReservation(
      string secret = "guest-secret",
      ReservationState state = ReservationState.Created)
  {
    DomainReservation r = new ReservationBuilder()
      .InState(state)
      .WithSecret(secret)
      .Build();
    Db.Reservations.Add(r);
    await Db.SaveChangesAsync();
    return r;
  }

  [Fact]
  public async Task Handle_MatchingSecret_TransitionsToCancelled_UsesProviderForTimestamp()
  {
    DomainReservation r = await SeedReservation();
    var cancelAt = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);
    Clock.Set(cancelAt);

    Result result = await CreateSut().Handle(
        new CancelReservationForGuestCommand(r.Id, "guest-secret"),
        CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation reloaded = await Db.Reservations.AsNoTracking().SingleAsync(x => x.Id == r.Id);
    reloaded.State.ShouldBe(ReservationState.Cancelled);
    reloaded.UpdatedAtUtc.ShouldBe(cancelAt);
  }

  [Fact]
  public async Task Handle_ReservationMissing_ReturnsNotFound()
  {
    var missing = Guid.NewGuid();

    Result result = await CreateSut().Handle(
        new CancelReservationForGuestCommand(missing, "any"), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.NotFound(missing));
  }

  [Fact]
  public async Task Handle_SecretMismatch_ReturnsSecretInvalid_DoesNotCancel()
  {
    DomainReservation r = await SeedReservation(secret: "real");

    Result result = await CreateSut().Handle(
        new CancelReservationForGuestCommand(r.Id, "wrong"), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.SecretInvalid);
    DomainReservation reloaded = await Db.Reservations.AsNoTracking().SingleAsync(x => x.Id == r.Id);
    reloaded.State.ShouldBe(ReservationState.Created);
    reloaded.UpdatedAtUtc.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_AlreadyCancelled_ReturnsAlreadyCancelled()
  {
    DomainReservation r = await SeedReservation(state: ReservationState.Cancelled);

    Result result = await CreateSut().Handle(
        new CancelReservationForGuestCommand(r.Id, "guest-secret"), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.AlreadyCancelled(r.Id));
  }

  [Fact]
  public async Task Handle_SecretMismatchOnAlreadyCancelled_SurfacesSecretInvalidFirst()
  {
    // Secret check precedes state check, so a wrong secret on a cancelled reservation
    // must not leak the reservation's state to an attacker.
    DomainReservation r = await SeedReservation(
        secret: "real", state: ReservationState.Cancelled);

    Result result = await CreateSut().Handle(
        new CancelReservationForGuestCommand(r.Id, "wrong"), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.SecretInvalid);
  }
}
