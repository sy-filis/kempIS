using Application.Services.Languages;
using Domain.Reservations.Nationalities;
using Domain.Services.Languages;
using SharedKernel;

namespace Application.UnitTests.Services.Languages;

public sealed class DeleteLanguageCommandHandlerTests : HandlerTestBase
{
  private DeleteLanguageCommandHandler CreateSut() => new(Db);

  [Fact]
  public async Task Handle_LanguageExists_NotReferenced_DeletesLanguage()
  {
    var id = Guid.NewGuid();
    Db.Languages.Add(new Language { Id = id, Code = "xx", Name = "Test" });
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(new DeleteLanguageCommand(id), CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    (await Db.Languages.FindAsync(id)).ShouldBeNull();
  }

  [Fact]
  public async Task Handle_LanguageNotFound_ReturnsNotFound()
  {
    Result result = await CreateSut()
      .Handle(new DeleteLanguageCommand(Guid.NewGuid()), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Language.NotFound");
  }

  [Fact]
  public async Task Handle_LanguageReferencedByNationality_ReturnsHasReferences()
  {
    var languageId = Guid.NewGuid();
    Db.Languages.Add(new Language { Id = languageId, Code = "xx", Name = "Test" });
    Db.Nationalities.Add(new Nationality
    {
      Id = Guid.NewGuid(),
      Name = "Testland",
      NameEn = "Test",
      Alpha2 = "TL",
      Alpha3 = "TLD",
      Numeric = "999",
      VisaRequired = false,
      BiometricsRequired = false,
      IsEu = false,
      LanguageId = languageId,
    });
    await Db.SaveChangesAsync();

    Result result = await CreateSut().Handle(new DeleteLanguageCommand(languageId), CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Code.ShouldBe("Language.HasReferences");
    (await Db.Languages.FindAsync(languageId)).ShouldNotBeNull();
  }
}
