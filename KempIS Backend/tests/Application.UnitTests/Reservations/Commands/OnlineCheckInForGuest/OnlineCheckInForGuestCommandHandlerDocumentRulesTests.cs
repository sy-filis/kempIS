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

public sealed class OnlineCheckInForGuestCommandHandlerDocumentRulesTests : HandlerTestBase
{
  private static readonly DateOnly StayStart = new(2026, 6, 1);
  private static readonly DateOnly StayEnd = new(2026, 6, 5);

  private static readonly RetentionSettings TestRetention = new()
  {
    GuestYears = 6,
    BillYears = 10,
    InvoiceYears = 10,
    RunAtLocalTime = new TimeOnly(3, 0),
  };

  private OnlineCheckInForGuestCommandHandler CreateSut() =>
    new(Db, Clock, Options.Create(TestRetention));

  private async Task<Nationality> SeedNationalityAsync(
    string alpha2,
    bool isEu = false,
    bool visaRequired = false,
    bool biometricsRequired = false)
  {
    Nationality n = new()
    {
      Id = Guid.NewGuid(),
      Name = alpha2,
      NameEn = "Test",
      Alpha2 = alpha2,
      Alpha3 = alpha2.PadRight(3, 'X'),
      Numeric = "000",
      VisaRequired = visaRequired,
      BiometricsRequired = biometricsRequired,
      IsEu = isEu,
      LanguageId = Guid.NewGuid(),
    };
    Db.Nationalities.Add(n);
    await Db.SaveChangesAsync();
    return n;
  }

  private async Task<DomainReservation> SeedReservationAsync(string secret)
  {
    DomainReservation r = new ReservationBuilder()
      .InState(ReservationState.Confirmed)
      .WithSecret(secret)
      .For(StayStart, StayEnd)
      .Build();
    Db.Reservations.Add(r);
    await Db.SaveChangesAsync();
    return r;
  }

  private static readonly byte[] PngMagic =
    [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

  private static string ValidPngBase64()
  {
    byte[] bytes = new byte[PngMagic.Length + 16];
    Array.Copy(PngMagic, bytes, PngMagic.Length);
    return Convert.ToBase64String(bytes);
  }

  private static OnlineCheckInGuest BuildGuest(
    Guid nationalityId,
    DocumentType? documentType,
    string? documentNumber,
    string? visaNumber = null,
    DateOnly? birthDate = null,
    string? signaturePngBase64 = null) => new(
      FirstName: "Jan",
      LastName: "Novak",
      BirthDate: birthDate ?? new DateOnly(1990, 1, 1),
      NationalityId: nationalityId,
      DocumentType: documentType,
      DocumentNumber: documentNumber,
      VisaNumber: visaNumber,
      Address: new Address(Guid.NewGuid(), "C", "12345", "S", "1"),
      SignaturePngBase64: signaturePngBase64);

  private async Task<Result> ExecuteAsync(
    DomainReservation reservation,
    string secret,
    OnlineCheckInGuest guest)
  {
    return await CreateSut().Handle(
      new OnlineCheckInForGuestCommand(reservation.Id, secret, [guest], []),
      CancellationToken.None);
  }

  [Fact]
  public async Task CzechAdult_WithPassport_Succeeds()
  {
    DomainReservation r = await SeedReservationAsync("s");
    Nationality cz = await SeedNationalityAsync("CZ");

    Result result = await ExecuteAsync(r, "s",
      BuildGuest(cz.Id, DocumentType.Passport, "P1"));

    result.IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public async Task CzechAdult_WithIdCard_Succeeds()
  {
    DomainReservation r = await SeedReservationAsync("s");
    Nationality cz = await SeedNationalityAsync("CZ");

    Result result = await ExecuteAsync(r, "s",
      BuildGuest(cz.Id, DocumentType.IdCard, "ID1"));

    result.IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public async Task CzechAdult_WithChildInParentPassport_ReturnsNotAllowed()
  {
    DomainReservation r = await SeedReservationAsync("s");
    Nationality cz = await SeedNationalityAsync("CZ");

    Result result = await ExecuteAsync(r, "s",
      BuildGuest(cz.Id, DocumentType.ChildInParentPassport, "X1"));

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(OnlineCheckInErrors.DocumentTypeNotAllowed(0, DocumentType.ChildInParentPassport, "CZ"));
  }

  [Fact]
  public async Task CzechAdult_WithoutDocument_ReturnsTypeRequired()
  {
    DomainReservation r = await SeedReservationAsync("s");
    Nationality cz = await SeedNationalityAsync("CZ");

    Result result = await ExecuteAsync(r, "s",
      BuildGuest(cz.Id, documentType: null, documentNumber: null));

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(OnlineCheckInErrors.DocumentTypeRequired(0));
  }

  [Fact]
  public async Task CzechMinor_WithoutDocument_Succeeds()
  {
    DomainReservation r = await SeedReservationAsync("s");
    Nationality cz = await SeedNationalityAsync("CZ");

    Result result = await ExecuteAsync(r, "s",
      BuildGuest(cz.Id, documentType: null, documentNumber: null,
        birthDate: new DateOnly(2015, 1, 1))); // age 11 at stay start

    result.IsSuccess.ShouldBeTrue();
    Guest persisted = await Db.Guests.AsNoTracking().SingleAsync();
    persisted.DocumentType.ShouldBeNull();
    persisted.DocumentNumber.ShouldBeNull();
  }

  [Fact]
  public async Task CzechMinor_WithDocumentNumberButNoType_ReturnsTypeRequired()
  {
    DomainReservation r = await SeedReservationAsync("s");
    Nationality cz = await SeedNationalityAsync("CZ");

    Result result = await ExecuteAsync(r, "s",
      BuildGuest(cz.Id, documentType: null, documentNumber: "P1",
        birthDate: new DateOnly(2015, 1, 1)));

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(OnlineCheckInErrors.DocumentTypeRequired(0));
  }

  [Fact]
  public async Task CzechMinor_TurnsFifteenOnStayStart_NotMinor()
  {
    DomainReservation r = await SeedReservationAsync("s");
    Nationality cz = await SeedNationalityAsync("CZ");

    // Born exactly 15 years before stay start → age 15 → not minor → doc required.
    Result result = await ExecuteAsync(r, "s",
      BuildGuest(cz.Id, documentType: null, documentNumber: null,
        birthDate: StayStart.AddYears(-15)));

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(OnlineCheckInErrors.DocumentTypeRequired(0));
  }

  [Fact]
  public async Task EuAdult_WithPassport_Succeeds()
  {
    DomainReservation r = await SeedReservationAsync("s");
    Nationality de = await SeedNationalityAsync("DE", isEu: true);

    Result result = await ExecuteAsync(r, "s",
      BuildGuest(de.Id, DocumentType.Passport, "P1", signaturePngBase64: ValidPngBase64()));

    result.IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public async Task EuAdult_WithChildInParentPassport_ReturnsNotAllowed()
  {
    DomainReservation r = await SeedReservationAsync("s");
    Nationality de = await SeedNationalityAsync("DE", isEu: true);

    Result result = await ExecuteAsync(r, "s",
      BuildGuest(de.Id, DocumentType.ChildInParentPassport, "X1"));

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(OnlineCheckInErrors.DocumentTypeNotAllowed(0, DocumentType.ChildInParentPassport, "DE"));
  }

  [Fact]
  public async Task NonEuAdult_WithIdCard_ReturnsNotAllowed()
  {
    DomainReservation r = await SeedReservationAsync("s");
    Nationality ru = await SeedNationalityAsync("RU", visaRequired: true);

    Result result = await ExecuteAsync(r, "s",
      BuildGuest(ru.Id, DocumentType.IdCard, "ID1"));

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(OnlineCheckInErrors.DocumentTypeNotAllowed(0, DocumentType.IdCard, "RU"));
  }

  [Fact]
  public async Task NonEuAdult_PassportVisaRequired_NoVisa_ReturnsVisaRequired()
  {
    DomainReservation r = await SeedReservationAsync("s");
    Nationality ru = await SeedNationalityAsync("RU", visaRequired: true);

    Result result = await ExecuteAsync(r, "s",
      BuildGuest(ru.Id, DocumentType.Passport, "P1"));

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(OnlineCheckInErrors.VisaNumberRequired(0));
  }

  [Fact]
  public async Task NonEuAdult_PassportVisaRequired_WithVisa_PersistsVisa()
  {
    DomainReservation r = await SeedReservationAsync("s");
    Nationality ru = await SeedNationalityAsync("RU", visaRequired: true);

    Result result = await ExecuteAsync(r, "s",
      BuildGuest(ru.Id, DocumentType.Passport, "P1", visaNumber: "RUS123456",
        signaturePngBase64: ValidPngBase64()));

    result.IsSuccess.ShouldBeTrue();
    Guest persisted = await Db.Guests.AsNoTracking().SingleAsync();
    persisted.VisaNumber.ShouldBe("RUS123456");
  }

  [Fact]
  public async Task NonEuAdult_PassportVisaNotRequired_NoVisa_Succeeds()
  {
    DomainReservation r = await SeedReservationAsync("s");
    Nationality us = await SeedNationalityAsync("US");

    Result result = await ExecuteAsync(r, "s",
      BuildGuest(us.Id, DocumentType.Passport, "P1", signaturePngBase64: ValidPngBase64()));

    result.IsSuccess.ShouldBeTrue();
  }

  [Fact]
  public async Task NonEuAdult_BiometrikaWithoutBiometricsExemption_ReturnsBiometrikaNotAllowed()
  {
    DomainReservation r = await SeedReservationAsync("s");
    Nationality ru = await SeedNationalityAsync("RU", visaRequired: true);

    Result result = await ExecuteAsync(r, "s",
      BuildGuest(ru.Id, DocumentType.Passport, "P1", visaNumber: "BIOMETRIKA"));

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(OnlineCheckInErrors.BiometrikaNotAllowed(0));
  }

  [Fact]
  public async Task NonEuAdult_BiometrikaWithBiometricsExemption_Succeeds()
  {
    DomainReservation r = await SeedReservationAsync("s");
    Nationality jp = await SeedNationalityAsync("JP", visaRequired: true, biometricsRequired: true);

    Result result = await ExecuteAsync(r, "s",
      BuildGuest(jp.Id, DocumentType.Passport, "P1", visaNumber: "BIOMETRIKA",
        signaturePngBase64: ValidPngBase64()));

    result.IsSuccess.ShouldBeTrue();
    Guest persisted = await Db.Guests.AsNoTracking().SingleAsync();
    persisted.VisaNumber.ShouldBe("BIOMETRIKA");
  }

  [Fact]
  public async Task DocumentType_Set_ButNumberMissing_ReturnsNumberRequired()
  {
    DomainReservation r = await SeedReservationAsync("s");
    Nationality cz = await SeedNationalityAsync("CZ");

    Result result = await ExecuteAsync(r, "s",
      BuildGuest(cz.Id, DocumentType.Passport, documentNumber: ""));

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(OnlineCheckInErrors.DocumentNumberRequired(0));
  }
}
