using Application.Reservations.Guests;
using Domain.Common;
using Domain.Reservations.Guests;
using Domain.Reservations.Nationalities;
using SharedKernel;

namespace Application.UnitTests.Reservations.Guests;

public sealed class UpdateGuestCommandHandlerSignatureTests : HandlerTestBase
{
  private static readonly byte[] PngMagic =
    [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

  private static string MakePng(byte payload)
  {
    byte[] bytes = new byte[PngMagic.Length + 1];
    Array.Copy(PngMagic, bytes, PngMagic.Length);
    bytes[^1] = payload;
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

  private async Task<Guest> SeedGuest(Guid nationalityId, byte[]? existingSignature, DateTime? existingTimestamp)
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
      SignaturePng = existingSignature,
      SignatureCapturedAtUtc = existingTimestamp,
    };
    Db.Guests.Add(g);
    await Db.SaveChangesAsync();
    return g;
  }

  private UpdateGuestCommandHandler CreateSut() => new(Db, Clock);

  private static UpdateGuestCommand BuildCommand(Guest g, string? signature) => new(
    Id: g.Id,
    ReservationId: g.ReservationId!.Value,
    BillId: null,
    PaysRecreationFee: null,
    FirstName: g.FirstName,
    LastName: g.LastName,
    NationalityId: g.NationalityId,
    DateOfBirth: g.DateOfBirth,
    DocumentType: g.DocumentType!.Value,
    DocumentNumber: g.DocumentNumber!,
    Address: g.Address,
    ReasonOfStay: g.ReasonOfStay,
    StayDateRange: g.StayDateRange,
    VisaNumber: null,
    Note: null,
    Scartation: null,
    CheckInAt: null,
    CheckOutAt: null,
    SignaturePngBase64: signature);

  [Fact]
  public async Task Handle_NullSignature_LeavesExistingUntouched()
  {
    Nationality de = await SeedNationality("DE");
    DateTime original = new(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
    byte[] originalBytes = [.. PngMagic, 0xAA];
    Guest g = await SeedGuest(de.Id, originalBytes, original);

    Result result = await CreateSut().Handle(BuildCommand(g, signature: null), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest reloaded = await Db.Guests.AsNoTracking().SingleAsync(x => x.Id == g.Id);
    reloaded.SignaturePng.ShouldBe(originalBytes);
    reloaded.SignatureCapturedAtUtc.ShouldBe(original);
  }

  [Fact]
  public async Task Handle_NewSignatureForNonCzech_ReplacesAndStampsTime()
  {
    Nationality de = await SeedNationality("DE");
    Guest g = await SeedGuest(de.Id, existingSignature: null, existingTimestamp: null);
    DateTime now = new(2026, 4, 28, 10, 0, 0, DateTimeKind.Utc);
    Clock.Set(now);

    Result result = await CreateSut().Handle(BuildCommand(g, MakePng(0xBB)), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest reloaded = await Db.Guests.AsNoTracking().SingleAsync(x => x.Id == g.Id);
    reloaded.SignaturePng.ShouldNotBeNull();
    reloaded.SignaturePng![^1].ShouldBe((byte)0xBB);
    reloaded.SignatureCapturedAtUtc.ShouldBe(now);
  }

  [Fact]
  public async Task Handle_NewSignatureForCzech_DropsSilentlyAndClearsExisting()
  {
    Nationality cz = await SeedNationality("CZ");
    Guest g = await SeedGuest(
      cz.Id,
      existingSignature: [.. PngMagic, 0xAA],
      existingTimestamp: new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc));

    Result result = await CreateSut().Handle(BuildCommand(g, MakePng(0xBB)), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Guest reloaded = await Db.Guests.AsNoTracking().SingleAsync(x => x.Id == g.Id);
    reloaded.SignaturePng.ShouldBeNull();
    reloaded.SignatureCapturedAtUtc.ShouldBeNull();
  }
}
