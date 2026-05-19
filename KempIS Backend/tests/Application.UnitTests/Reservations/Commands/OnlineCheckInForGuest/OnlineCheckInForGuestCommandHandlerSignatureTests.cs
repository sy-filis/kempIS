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

public sealed class OnlineCheckInForGuestCommandHandlerSignatureTests : HandlerTestBase
{
  private static readonly byte[] PngMagic =
    [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

  private static readonly RetentionSettings TestRetention = new()
  {
    GuestYears = 6,
    BillYears = 10,
    InvoiceYears = 10,
    RunAtLocalTime = new TimeOnly(3, 0),
  };

  private static string ValidPngBase64()
  {
    byte[] bytes = new byte[PngMagic.Length + 16];
    Array.Copy(PngMagic, bytes, PngMagic.Length);
    return Convert.ToBase64String(bytes);
  }

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
      IsEu = false,
      LanguageId = Guid.NewGuid(),
    };
    Db.Nationalities.Add(n);
    await Db.SaveChangesAsync();
    return n;
  }

  private async Task<DomainReservation> SeedReservation(string secret)
  {
    DomainReservation r = new ReservationBuilder()
      .InState(ReservationState.Confirmed)
      .WithSecret(secret)
      .Build();
    Db.Reservations.Add(r);
    await Db.SaveChangesAsync();
    return r;
  }

  private static OnlineCheckInGuest BuildGuest(Guid nationalityId, string? signature) => new(
    FirstName: "Jan",
    LastName: "Novak",
    BirthDate: new DateOnly(1990, 1, 1),
    NationalityId: nationalityId,
    DocumentType: DocumentType.Passport,
    DocumentNumber: "P1",
    VisaNumber: null,
    Address: new Address(Guid.NewGuid(), "C", "12345", "S", "1"),
    SignaturePngBase64: signature);

  [Fact]
  public async Task Handle_NonCzechMissingSignature_ReturnsSignatureRequired()
  {
    DomainReservation r = await SeedReservation("s1");
    Nationality de = await SeedNationality("DE");

    Result result = await CreateSut().Handle(
      new OnlineCheckInForGuestCommand(r.Id, "s1",
        Guests: [BuildGuest(de.Id, signature: null)],
        Vehicles: []),
      CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(OnlineCheckInErrors.SignatureRequired(0));
    (await Db.Guests.AsNoTracking().CountAsync()).ShouldBe(0);
  }

  [Fact]
  public async Task Handle_NonCzechWithSignature_PersistsBytes()
  {
    DomainReservation r = await SeedReservation("s2");
    Nationality de = await SeedNationality("DE");
    DateTime now = new(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc);
    Clock.Set(now);

    Result result = await CreateSut().Handle(
      new OnlineCheckInForGuestCommand(r.Id, "s2",
        Guests: [BuildGuest(de.Id, ValidPngBase64())],
        Vehicles: []),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest g = await Db.Guests.AsNoTracking().SingleAsync();
    g.SignaturePng.ShouldNotBeNull();
    g.SignatureCapturedAtUtc.ShouldBe(now);
  }

  [Fact]
  public async Task Handle_CzechWithSignature_DropsSilently()
  {
    DomainReservation r = await SeedReservation("s3");
    Nationality cz = await SeedNationality("CZ");

    Result result = await CreateSut().Handle(
      new OnlineCheckInForGuestCommand(r.Id, "s3",
        Guests: [BuildGuest(cz.Id, ValidPngBase64())],
        Vehicles: []),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest g = await Db.Guests.AsNoTracking().SingleAsync();
    g.SignaturePng.ShouldBeNull();
    g.SignatureCapturedAtUtc.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_CzechWithoutSignature_Succeeds()
  {
    DomainReservation r = await SeedReservation("s4");
    Nationality cz = await SeedNationality("CZ");

    Result result = await CreateSut().Handle(
      new OnlineCheckInForGuestCommand(r.Id, "s4",
        Guests: [BuildGuest(cz.Id, signature: null)],
        Vehicles: []),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
  }
}
