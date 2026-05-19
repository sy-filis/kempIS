using Application.Reservations.Guests.Commands.SetGuestSignature;
using Domain.Common;
using Domain.Reservations.Guests;
using Domain.Reservations.Nationalities;
using SharedKernel;

namespace Application.UnitTests.Reservations.Guests;

public sealed class SetGuestSignatureCommandHandlerTests : HandlerTestBase
{
  private static readonly byte[] PngMagic =
    [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

  private static string ValidPngBase64()
  {
    byte[] bytes = new byte[PngMagic.Length + 16];
    Array.Copy(PngMagic, bytes, PngMagic.Length);
    return Convert.ToBase64String(bytes);
  }

  private SetGuestSignatureCommandHandler CreateSut() => new(Db, Clock);

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

  private async Task<Guest> SeedGuest(Guid nationalityId)
  {
    Guest g = new()
    {
      Id = Guid.NewGuid(),
      ReservationId = Guid.NewGuid(),
      FirstName = "A",
      LastName = "B",
      NationalityId = nationalityId,
      DateOfBirth = new DateOnly(1990, 1, 1),
      DocumentType = DocumentType.Passport,
      DocumentNumber = "P1",
      Address = new Address(Guid.NewGuid(), "City", "12345", "Street", "1"),
      ReasonOfStay = "tourism",
      StayDateRange = new DateRange(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 5)),
    };
    Db.Guests.Add(g);
    await Db.SaveChangesAsync();
    return g;
  }

  [Fact]
  public async Task Handle_NonCzechGuest_PersistsBytesAndStampsTime()
  {
    Nationality de = await SeedNationality("DE");
    Guest g = await SeedGuest(de.Id);
    DateTime now = new(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc);
    Clock.Set(now);

    Result result = await CreateSut().Handle(
      new SetGuestSignatureCommand(g.Id, ValidPngBase64()),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest reloaded = await Db.Guests.AsNoTracking().SingleAsync(x => x.Id == g.Id);
    reloaded.SignaturePng.ShouldNotBeNull();
    reloaded.SignaturePng!.Length.ShouldBe(PngMagic.Length + 16);
    reloaded.SignatureCapturedAtUtc.ShouldBe(now);
  }

  [Fact]
  public async Task Handle_CzechGuest_DropsSilentlyAndReturnsSuccess()
  {
    Nationality cz = await SeedNationality("CZ");
    Guest g = await SeedGuest(cz.Id);

    Result result = await CreateSut().Handle(
      new SetGuestSignatureCommand(g.Id, ValidPngBase64()),
      CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest reloaded = await Db.Guests.AsNoTracking().SingleAsync(x => x.Id == g.Id);
    reloaded.SignaturePng.ShouldBeNull();
    reloaded.SignatureCapturedAtUtc.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_GuestNotFound_ReturnsNotFound()
  {
    var missing = Guid.NewGuid();

    Result result = await CreateSut().Handle(
      new SetGuestSignatureCommand(missing, ValidPngBase64()),
      CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.ShouldBe(GuestErrors.NotFound(missing));
  }
}
