using Application.Reservations.Nationalities;
using Domain.Reservations.Nationalities;
using Domain.Services.Languages;
using SharedKernel;

namespace Application.UnitTests.Reservations.Nationalities;

public sealed class CreateNationalityCommandHandlerTests : HandlerTestBase
{
  private CreateNationalityCommandHandler CreateSut() => new(Db);

  private async Task<Guid> SeedLanguageAsync()
  {
    var id = Guid.NewGuid();
    Db.Languages.Add(new Language { Id = id, Code = "xx", Name = "Test" });
    await Db.SaveChangesAsync();
    return id;
  }

  private static CreateNationalityCommand Cmd(
    Guid languageId,
    string alpha2 = "TL",
    string alpha3 = "TLD",
    string numeric = "999")
    => new("Testland", "Testland EN", alpha2, alpha3, numeric, false, false, false, languageId);

  [Fact]
  public async Task Handle_ValidInput_InsertsNationality()
  {
    Guid languageId = await SeedLanguageAsync();

    Result<Guid> result = await CreateSut().Handle(Cmd(languageId), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    Nationality? saved = await Db.Nationalities.FindAsync(result.Value);
    saved.ShouldNotBeNull();
    saved!.Name.ShouldBe("Testland");
    saved.NameEn.ShouldBe("Testland EN");
    saved.Alpha2.ShouldBe("TL");
    saved.Alpha3.ShouldBe("TLD");
    saved.Numeric.ShouldBe("999");
    saved.LanguageId.ShouldBe(languageId);
  }

  [Fact]
  public async Task Handle_UnknownLanguage_ReturnsLanguageNotFound()
  {
    Result<Guid> result = await CreateSut().Handle(Cmd(Guid.NewGuid()), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Language.NotFound");
  }

  [Fact]
  public async Task Handle_DuplicateAlpha2_ReturnsConflict()
  {
    Guid languageId = await SeedLanguageAsync();
    await CreateSut().Handle(Cmd(languageId, alpha2: "TL", alpha3: "TLA", numeric: "001"), CancellationToken.None);

    Result<Guid> result = await CreateSut()
      .Handle(Cmd(languageId, alpha2: "TL", alpha3: "TLB", numeric: "002"), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Nationalities.Alpha2Exists");
  }

  [Fact]
  public async Task Handle_DuplicateAlpha3_ReturnsConflict()
  {
    Guid languageId = await SeedLanguageAsync();
    await CreateSut().Handle(Cmd(languageId, alpha2: "TA", alpha3: "TLD", numeric: "001"), CancellationToken.None);

    Result<Guid> result = await CreateSut()
      .Handle(Cmd(languageId, alpha2: "TB", alpha3: "TLD", numeric: "002"), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Nationalities.Alpha3Exists");
  }

  [Fact]
  public async Task Handle_DuplicateNumeric_ReturnsConflict()
  {
    Guid languageId = await SeedLanguageAsync();
    await CreateSut().Handle(Cmd(languageId, alpha2: "TA", alpha3: "TLA", numeric: "999"), CancellationToken.None);

    Result<Guid> result = await CreateSut()
      .Handle(Cmd(languageId, alpha2: "TB", alpha3: "TLB", numeric: "999"), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Nationalities.NumericExists");
  }
}
