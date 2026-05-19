using Application.Configuration;
using Application.Reservations.Commands.OnlineCheckInForGuest;
using Domain.Common;
using Domain.Reservations.Guests;
using Domain.Reservations.Nationalities;
using Domain.Reservations.ReservationStates;
using Microsoft.Extensions.Options;
using SharedKernel;
using DomainReservation = Domain.Reservations.Reservations.Reservation;

namespace Application.UnitTests.Reservations.Commands.OnlineCheckInForGuest;

public sealed class OnlineCheckInForGuestCommandHandlerRetentionTests : HandlerTestBase
{
  private static readonly RetentionSettings TestRetention = new()
  {
    GuestYears = 6,
    BillYears = 10,
    InvoiceYears = 10,
    RunAtLocalTime = new TimeOnly(3, 0),
  };

  private OnlineCheckInForGuestCommandHandler CreateSut() =>
    new(Db, Clock, Options.Create(TestRetention));

  private async Task<Nationality> SeedNationality(string alpha2)
  {
    Nationality n = new()
    {
      Id = Guid.NewGuid(),
      Name = alpha2,
      NameEn = "Test",
      Alpha2 = alpha2,
      Alpha3 = alpha2.PadRight(3, 'X'),
      Numeric = "000",
      VisaRequired = false,
      BiometricsRequired = false,
      IsEu = true,
      LanguageId = Guid.NewGuid(),
    };
    Db.Nationalities.Add(n);
    await Db.SaveChangesAsync();
    return n;
  }

  private async Task<DomainReservation> SeedReservation(string secret, DateOnly from, DateOnly to)
  {
    DomainReservation r = new ReservationBuilder()
      .InState(ReservationState.Confirmed)
      .WithSecret(secret)
      .For(from, to)
      .Build();
    Db.Reservations.Add(r);
    await Db.SaveChangesAsync();
    return r;
  }

  [Fact]
  public async Task Handle_SetsScartationOnEachReplacedGuest_FromConfig()
  {
    var from = new DateOnly(2026, 6, 1);
    var to = new DateOnly(2026, 6, 5);
    DomainReservation r = await SeedReservation("s-retention", from, to);
    Nationality cz = await SeedNationality("CZ");

    OnlineCheckInGuest BuildGuest(string firstName) => new(
      FirstName: firstName,
      LastName: "Novak",
      BirthDate: new DateOnly(1990, 1, 1),
      NationalityId: cz.Id,
      DocumentType: DocumentType.IdCard,
      DocumentNumber: $"ID-{firstName}",
      VisaNumber: null,
      Address: new Address(Guid.NewGuid(), "C", "12345", "S", "1"),
      SignaturePngBase64: null);

    Result result = await CreateSut().Handle(
      new OnlineCheckInForGuestCommand(r.Id, "s-retention",
        Guests: [BuildGuest("Anna"), BuildGuest("Petr")],
        Vehicles: []),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    List<Guest> persisted = await Db.Guests.AsNoTracking().ToListAsync();
    persisted.Count.ShouldBe(2);
    persisted.ShouldAllBe(g => g.Scartation == to.AddYears(TestRetention.GuestYears));
  }
}
