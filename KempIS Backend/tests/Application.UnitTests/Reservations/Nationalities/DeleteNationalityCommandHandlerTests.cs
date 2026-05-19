using Application.Reservations.Nationalities;
using Domain.Common;
using Domain.Reservations.Guests;
using Domain.Reservations.Nationalities;
using Domain.Services.Languages;
using SharedKernel;

namespace Application.UnitTests.Reservations.Nationalities;

public sealed class DeleteNationalityCommandHandlerTests : HandlerTestBase
{
  private DeleteNationalityCommandHandler CreateSut() => new(Db);

  private async Task<Guid> SeedNationalityAsync()
  {
    var languageId = Guid.NewGuid();
    Db.Languages.Add(new Language { Id = languageId, Code = "xx", Name = "Test" });
    var id = Guid.NewGuid();
    Db.Nationalities.Add(new Nationality
    {
      Id = id,
      Name = "Testland",
      NameEn = "Testland EN",
      Alpha2 = "TL",
      Alpha3 = "TLD",
      Numeric = "999",
      VisaRequired = false,
      BiometricsRequired = false,
      IsEu = false,
      LanguageId = languageId,
    });
    await Db.SaveChangesAsync();
    return id;
  }

  [Fact]
  public async Task Handle_NationalityExists_NotReferenced_DeletesNationality()
  {
    Guid id = await SeedNationalityAsync();

    Result result = await CreateSut().Handle(new DeleteNationalityCommand(id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    (await Db.Nationalities.FindAsync(id)).ShouldBeNull();
  }

  [Fact]
  public async Task Handle_NotFound_ReturnsNotFound()
  {
    Result result = await CreateSut()
      .Handle(new DeleteNationalityCommand(Guid.NewGuid()), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Nationality.NotFound");
  }

  [Fact]
  public async Task Handle_ReferencedByGuest_ReturnsHasReferences()
  {
    Guid nationalityId = await SeedNationalityAsync();
    Db.Guests.Add(new Guest
    {
      Id = Guid.NewGuid(),
      ReservationId = Guid.NewGuid(),
      BillId = null,
      FirstName = "Jan",
      LastName = "Novak",
      NationalityId = nationalityId,
      DateOfBirth = new DateOnly(1990, 1, 1),
      DocumentType = DocumentType.Passport,
      DocumentNumber = "AB123456",
      Address = new Address(Guid.NewGuid(), "Brno", "60200", "Kolejni", "2"),
      ReasonOfStay = "Holiday",
      StayDateRange = new DateRange(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5)),
      VisaNumber = null,
      Note = null,
      Scartation = null,
      CheckInAt = null,
      CheckOutAt = null,
    });
    await Db.SaveChangesAsync();

    Result result = await CreateSut()
      .Handle(new DeleteNationalityCommand(nationalityId), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Nationality.HasReferences");
    (await Db.Nationalities.FindAsync(nationalityId)).ShouldNotBeNull();
  }
}
