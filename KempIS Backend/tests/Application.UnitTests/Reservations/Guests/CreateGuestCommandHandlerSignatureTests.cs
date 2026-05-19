using Application.Configuration;
using Application.Reservations.Guests;
using Domain.Common;
using Domain.Reservations.Guests;
using Domain.Reservations.Nationalities;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.UnitTests.Reservations.Guests;

public sealed class CreateGuestCommandHandlerSignatureTests : HandlerTestBase
{
  private static readonly byte[] PngMagic =
    [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

  private static string ValidPngBase64()
  {
    byte[] bytes = new byte[PngMagic.Length + 16];
    Array.Copy(PngMagic, bytes, PngMagic.Length);
    return Convert.ToBase64String(bytes);
  }

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

  private static readonly RetentionSettings TestRetention = new()
  {
    GuestYears = 6,
    BillYears = 10,
    InvoiceYears = 10,
    RunAtLocalTime = new TimeOnly(3, 0),
  };

  private CreateGuestCommandHandler CreateSut() =>
    new(Db, Clock, Options.Create(TestRetention));

  private CreateGuestCommand BuildCommand(Guid nationalityId, string? signature) => new(
    ReservationId: Guid.NewGuid(),
    BillId: null,
    PaysRecreationFee: null,
    FirstName: "A",
    LastName: "B",
    NationalityId: nationalityId,
    DateOfBirth: new DateOnly(1990, 1, 1),
    DocumentType: DocumentType.Passport,
    DocumentNumber: "X1",
    Address: new Address(Guid.NewGuid(), "C", "12345", "S", "1"),
    ReasonOfStay: "tourism",
    StayDateRange: new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
    VisaNumber: null,
    Note: null,
    Scartation: null,
    CheckInAt: null,
    CheckOutAt: null,
    SignaturePngBase64: signature);

  [Fact]
  public async Task Handle_NonCzechWithSignature_StoresBytes()
  {
    Nationality de = await SeedNationality("DE");
    DateTime now = new(2026, 4, 27, 10, 0, 0, DateTimeKind.Utc);
    Clock.Set(now);

    Result<Guid> result = await CreateSut().Handle(
      BuildCommand(de.Id, ValidPngBase64()), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest reloaded = await Db.Guests.AsNoTracking().SingleAsync(x => x.Id == result.Value);
    reloaded.SignaturePng.ShouldNotBeNull();
    reloaded.SignatureCapturedAtUtc.ShouldBe(now);
  }

  [Fact]
  public async Task Handle_CzechWithSignature_DropsSilently()
  {
    Nationality cz = await SeedNationality("CZ");

    Result<Guid> result = await CreateSut().Handle(
      BuildCommand(cz.Id, ValidPngBase64()), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest reloaded = await Db.Guests.AsNoTracking().SingleAsync(x => x.Id == result.Value);
    reloaded.SignaturePng.ShouldBeNull();
    reloaded.SignatureCapturedAtUtc.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_NonCzechWithoutSignature_StoresNoSignature()
  {
    Nationality de = await SeedNationality("DE");

    Result<Guid> result = await CreateSut().Handle(
      BuildCommand(de.Id, null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest reloaded = await Db.Guests.AsNoTracking().SingleAsync(x => x.Id == result.Value);
    reloaded.SignaturePng.ShouldBeNull();
  }
}
