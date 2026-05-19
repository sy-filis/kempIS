using Application.Reservations.Commands.CheckInReservation;
using Domain.Common;
using Domain.Reservations;
using Domain.Reservations.Guests;
using Domain.Reservations.Nationalities;
using Domain.Reservations.ReservationStates;
using SharedKernel;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Application.UnitTests.Reservations.Commands.CheckInReservation;

public sealed class CheckInReservationSignatureGateTests : HandlerTestBase
{
  private CheckInReservationCommandHandler CreateSut() => new(Db, Clock);

  private async Task<Nationality> SeedNationality(string alpha2)
  {
    Nationality n = new()
    {
      Id = Guid.NewGuid(),
      Name = alpha2,
      NameEn = "Test",
      Alpha2 = alpha2,
      Alpha3 = alpha2.PadRight(3, 'X'),
      Numeric = alpha2.PadLeft(3, '0'),
      VisaRequired = false,
      BiometricsRequired = false,
      IsEu = false,
      LanguageId = Guid.NewGuid(),
    };
    Db.Nationalities.Add(n);
    await Db.SaveChangesAsync();
    return n;
  }

  private async Task<DomainReservation> SeedReservation()
  {
    DomainReservation r = new ReservationBuilder()
      .InState(ReservationState.Confirmed)
      .Build();
    Db.Reservations.Add(r);
    await Db.SaveChangesAsync();
    return r;
  }

  private async Task SeedGuest(Guid reservationId, Guid nationalityId, byte[]? signature)
  {
    Db.Guests.Add(new Guest
    {
      Id = Guid.NewGuid(),
      ReservationId = reservationId,
      FirstName = "A",
      LastName = "B",
      NationalityId = nationalityId,
      DateOfBirth = new DateOnly(1990, 1, 1),
      DocumentType = DocumentType.Passport,
      DocumentNumber = "P1",
      Address = new Address(Guid.NewGuid(), "City", "12345", "Street", "1"),
      ReasonOfStay = "tourism",
      StayDateRange = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
      SignaturePng = signature,
    });
    await Db.SaveChangesAsync();
  }

  private static byte[] AnyPng() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0xAA];

  [Fact]
  public async Task Handle_AllNonCzechSigned_Succeeds()
  {
    DomainReservation r = await SeedReservation();
    Nationality de = await SeedNationality("DE");
    await SeedGuest(r.Id, de.Id, AnyPng());

    Result result = await CreateSut().Handle(new CheckInReservationCommand(r.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    DomainReservation reloaded = await Db.Reservations.AsNoTracking().SingleAsync(x => x.Id == r.Id);
    reloaded.State.ShouldBe(ReservationState.CheckedIn);
  }

  [Fact]
  public async Task Handle_OnlyCzechGuests_NoSignaturesNeeded_Succeeds()
  {
    DomainReservation r = await SeedReservation();
    Nationality cz = await SeedNationality("CZ");
    await SeedGuest(r.Id, cz.Id, signature: null);

    Result result = await CreateSut().Handle(new CheckInReservationCommand(r.Id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public async Task Handle_NonCzechMissingSignature_FailsWithGuestIds()
  {
    DomainReservation r = await SeedReservation();
    Nationality de = await SeedNationality("DE");
    await SeedGuest(r.Id, de.Id, signature: null);
    Guid missingId = await Db.Guests.AsNoTracking().Select(g => g.Id).SingleAsync();

    Result result = await CreateSut().Handle(new CheckInReservationCommand(r.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(ReservationErrors.MissingGuestSignatures([missingId]));
    DomainReservation reloaded = await Db.Reservations.AsNoTracking().SingleAsync(x => x.Id == r.Id);
    reloaded.State.ShouldBe(ReservationState.Confirmed);
  }

  [Fact]
  public async Task Handle_MixedCzechAndNonCzech_OnlyNonCzechMissingFails()
  {
    DomainReservation r = await SeedReservation();
    Nationality cz = await SeedNationality("CZ");
    Nationality de = await SeedNationality("DE");
    await SeedGuest(r.Id, cz.Id, signature: null);
    await SeedGuest(r.Id, de.Id, signature: null);

    Result result = await CreateSut().Handle(new CheckInReservationCommand(r.Id), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Reservation.MissingGuestSignatures");
  }
}
