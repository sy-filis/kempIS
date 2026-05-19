using Application.Reservations.Nationalities;
using Domain.Reservations.Nationalities;
using Domain.Services.Languages;
using SharedKernel;

namespace Application.UnitTests.Reservations.Nationalities;

public sealed class UpdateNationalityCommandHandlerTests : HandlerTestBase
{
  private UpdateNationalityCommandHandler CreateSut() => new(Db);

  private async Task<Guid> SeedLanguageAsync()
  {
    var id = Guid.NewGuid();
    Db.Languages.Add(new Language { Id = id, Code = "xx", Name = "Test" });
    await Db.SaveChangesAsync();
    return id;
  }

  private async Task<Guid> SeedNationalityAsync(
    Guid languageId,
    string alpha2 = "TL",
    string alpha3 = "TLD",
    string numeric = "999")
  {
    var id = Guid.NewGuid();
    Db.Nationalities.Add(new Nationality
    {
      Id = id,
      Name = "Testland",
      NameEn = "Testland EN",
      Alpha2 = alpha2,
      Alpha3 = alpha3,
      Numeric = numeric,
      VisaRequired = false,
      BiometricsRequired = false,
      IsEu = false,
      LanguageId = languageId,
    });
    await Db.SaveChangesAsync();
    return id;
  }

  [Fact]
  public async Task Handle_ValidInput_UpdatesNationality()
  {
    Guid languageId = await SeedLanguageAsync();
    Guid nationalityId = await SeedNationalityAsync(languageId);

    UpdateNationalityCommand command = new(
      nationalityId, "Renamed", "Renamed EN", "RN", "RND", "001", true, true, true, languageId);

    Result result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Nationality? updated = await Db.Nationalities.FindAsync(nationalityId);
    updated!.Name.ShouldBe("Renamed");
    updated.NameEn.ShouldBe("Renamed EN");
    updated.Alpha2.ShouldBe("RN");
    updated.Alpha3.ShouldBe("RND");
    updated.Numeric.ShouldBe("001");
    updated.VisaRequired.ShouldBeTrue();
    updated.BiometricsRequired.ShouldBeTrue();
    updated.IsEu.ShouldBeTrue();
    updated.LanguageId.ShouldBe(languageId);
  }

  [Fact]
  public async Task Handle_NotFound_ReturnsNotFound()
  {
    Guid languageId = await SeedLanguageAsync();

    UpdateNationalityCommand command = new(
      Guid.NewGuid(), "Renamed", "Renamed EN", "RN", "RND", "001", false, false, false, languageId);

    Result result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Nationality.NotFound");
  }

  [Fact]
  public async Task Handle_UnknownLanguage_ReturnsLanguageNotFound()
  {
    Guid languageId = await SeedLanguageAsync();
    Guid nationalityId = await SeedNationalityAsync(languageId);

    UpdateNationalityCommand command = new(
      nationalityId, "Renamed", "Renamed EN", "RN", "RND", "001", false, false, false, Guid.NewGuid());

    Result result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Language.NotFound");
  }

  [Fact]
  public async Task Handle_DuplicateAlpha3_OnDifferentRow_ReturnsConflict()
  {
    Guid languageId = await SeedLanguageAsync();
    await SeedNationalityAsync(languageId, "AA", "AAA", "001");
    Guid secondId = await SeedNationalityAsync(languageId, "BB", "BBB", "002");

    UpdateNationalityCommand command = new(
      secondId, "Renamed", "Renamed EN", "BB", "AAA", "002", false, false, false, languageId);

    Result result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Nationalities.Alpha3Exists");
  }

  [Fact]
  public async Task Handle_DuplicateAlpha2_OnDifferentRow_ReturnsConflict()
  {
    Guid languageId = await SeedLanguageAsync();
    await SeedNationalityAsync(languageId, "AA", "AAA", "001");
    Guid secondId = await SeedNationalityAsync(languageId, "BB", "BBB", "002");

    UpdateNationalityCommand command = new(
      secondId, "Renamed", "Renamed EN", "AA", "BBB", "002", false, false, false, languageId);

    Result result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Nationalities.Alpha2Exists");
  }

  [Fact]
  public async Task Handle_DuplicateNumeric_OnDifferentRow_ReturnsConflict()
  {
    Guid languageId = await SeedLanguageAsync();
    await SeedNationalityAsync(languageId, "AA", "AAA", "001");
    Guid secondId = await SeedNationalityAsync(languageId, "BB", "BBB", "002");

    UpdateNationalityCommand command = new(
      secondId, "Renamed", "Renamed EN", "BB", "BBB", "001", false, false, false, languageId);

    Result result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Nationalities.NumericExists");
  }

  [Fact]
  public async Task Handle_SameAlpha3_OnSameRow_Succeeds()
  {
    Guid languageId = await SeedLanguageAsync();
    Guid id = await SeedNationalityAsync(languageId, "AA", "AAA", "001");

    UpdateNationalityCommand command = new(
      id, "Renamed", "Renamed EN", "AA", "AAA", "001", true, false, false, languageId);

    Result result = await CreateSut().Handle(command, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
  }
}
