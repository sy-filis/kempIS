using Domain.Reservations.Nationalities;
using Domain.Services.Languages;
using Infrastructure.Seed;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infrastructure.UnitTests.Seed;

public sealed class ReferenceDataSeederTests : HandlerTestBase
{
  [Fact]
  public async Task SeedAsync_EmptyTables_SeedsLanguagesAndNationalities()
  {
    ReferenceDataSeeder seeder = new(Db, NullLogger<ReferenceDataSeeder>.Instance);

    await seeder.SeedAsync(CancellationToken.None);

    List<Language> languages = await Db.Languages.AsNoTracking().ToListAsync();
    List<Nationality> nationalities = await Db.Nationalities.AsNoTracking().ToListAsync();

    languages.Count.ShouldBe(2);
    languages.ShouldContain(l => l.Code == "en" && l.Name == "English");
    languages.ShouldContain(l => l.Code == "cs" && l.Name == "Čeština");

    nationalities.Count.ShouldBe(249);

    Nationality cze = nationalities.Single(n => n.Alpha3 == "CZE");
    cze.Name.ShouldBe("Česko");
    cze.NameEn.ShouldBe("Czechia");
    cze.Alpha2.ShouldBe("CZ");
    cze.Numeric.ShouldBe("203");
    cze.VisaRequired.ShouldBeFalse();
    cze.BiometricsRequired.ShouldBeFalse();
    cze.IsEu.ShouldBeTrue();
    cze.LanguageId.ShouldBe(ReferenceDataSeeder.CzechLanguageId);

    Nationality svk = nationalities.Single(n => n.Alpha3 == "SVK");
    svk.NameEn.ShouldBe("Slovakia");
    svk.LanguageId.ShouldBe(ReferenceDataSeeder.CzechLanguageId);
    svk.IsEu.ShouldBeTrue();

    Nationality deu = nationalities.Single(n => n.Alpha3 == "DEU");
    deu.NameEn.ShouldBe("Germany");
    deu.LanguageId.ShouldBe(ReferenceDataSeeder.EnglishLanguageId);
    deu.IsEu.ShouldBeTrue();

    Nationality afg = nationalities.Single(n => n.Alpha3 == "AFG");
    afg.VisaRequired.ShouldBeTrue();
    afg.IsEu.ShouldBeFalse();

    Nationality alb = nationalities.Single(n => n.Alpha3 == "ALB");
    alb.BiometricsRequired.ShouldBeTrue();
    alb.IsEu.ShouldBeFalse();

    Nationality che = nationalities.Single(n => n.Alpha3 == "CHE");
    che.IsEu.ShouldBeFalse();

    Nationality nor = nationalities.Single(n => n.Alpha3 == "NOR");
    nor.IsEu.ShouldBeFalse();

    Nationality gbr = nationalities.Single(n => n.Alpha3 == "GBR");
    gbr.IsEu.ShouldBeFalse();

    nationalities.Count(n => n.IsEu).ShouldBe(27);
  }

  [Fact]
  public async Task SeedAsync_NonEmptyLanguages_DoesNotInsertLanguages()
  {
    Db.Languages.Add(new Language { Id = Guid.NewGuid(), Code = "fr", Name = "Français" });
    await Db.SaveChangesAsync();

    ReferenceDataSeeder seeder = new(Db, NullLogger<ReferenceDataSeeder>.Instance);

    await seeder.SeedAsync(CancellationToken.None);

    List<Language> languages = await Db.Languages.AsNoTracking().ToListAsync();
    languages.Count.ShouldBe(1);
    languages[0].Code.ShouldBe("fr");
  }

  [Fact]
  public async Task SeedAsync_NonEmptyNationalities_DoesNotInsertNationalities()
  {
    var languageId = Guid.NewGuid();
    Db.Languages.Add(new Language { Id = languageId, Code = "xx", Name = "Test" });
    Db.Nationalities.Add(new Nationality
    {
      Id = Guid.NewGuid(),
      Name = "Preexisting",
      NameEn = "Preexisting",
      Alpha2 = "PE",
      Alpha3 = "PRE",
      Numeric = "999",
      VisaRequired = false,
      BiometricsRequired = false,
      IsEu = false,
      LanguageId = languageId,
    });
    await Db.SaveChangesAsync();

    ReferenceDataSeeder seeder = new(Db, NullLogger<ReferenceDataSeeder>.Instance);

    await seeder.SeedAsync(CancellationToken.None);

    List<Nationality> nationalities = await Db.Nationalities.AsNoTracking().ToListAsync();
    nationalities.Count.ShouldBe(1);
    nationalities[0].Alpha3.ShouldBe("PRE");
  }

  [Fact]
  public async Task SeedAsync_SecondCall_IsNoOp()
  {
    ReferenceDataSeeder seeder = new(Db, NullLogger<ReferenceDataSeeder>.Instance);
    await seeder.SeedAsync(CancellationToken.None);

    int langsAfterFirst = await Db.Languages.CountAsync();
    int natsAfterFirst = await Db.Nationalities.CountAsync();

    await seeder.SeedAsync(CancellationToken.None);

    (await Db.Languages.CountAsync()).ShouldBe(langsAfterFirst);
    (await Db.Nationalities.CountAsync()).ShouldBe(natsAfterFirst);
  }
}
