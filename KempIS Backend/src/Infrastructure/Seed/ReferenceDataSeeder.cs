using Domain.Reservations.Nationalities;
using Domain.Services.Languages;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Seed;

internal sealed class ReferenceDataSeeder
{
  public static readonly Guid EnglishLanguageId = new("11111111-1111-1111-1111-111111111111");
  public static readonly Guid CzechLanguageId = new("22222222-2222-2222-2222-222222222222");

  private readonly ApplicationDbContext _db;
  private readonly ILogger<ReferenceDataSeeder> _logger;

  public ReferenceDataSeeder(ApplicationDbContext db, ILogger<ReferenceDataSeeder> logger)
  {
    _db = db;
    _logger = logger;
  }

  public async Task SeedAsync(CancellationToken cancellationToken)
  {
    await SeedLanguagesAsync(cancellationToken);
    await SeedNationalitiesAsync(cancellationToken);
  }

  private async Task SeedLanguagesAsync(CancellationToken ct)
  {
    if (await _db.Languages.AnyAsync(ct))
    {
      _logger.LogDebug("Languages table not empty; skipping language seed");
      return;
    }

    _db.Languages.Add(new Language { Id = EnglishLanguageId, Code = "en", Name = "English" });
    _db.Languages.Add(new Language { Id = CzechLanguageId, Code = "cs", Name = "Čeština" });
    await _db.SaveChangesAsync(ct);

    _logger.LogInformation("Seeded 2 languages (en, cs)");
  }

  private async Task SeedNationalitiesAsync(CancellationToken ct)
  {
    if (await _db.Nationalities.AnyAsync(ct))
    {
      _logger.LogDebug("Nationalities table not empty; skipping nationality seed");
      return;
    }

    foreach (Countries.CountrySeed c in Countries.All)
    {
      _db.Nationalities.Add(new Nationality
      {
        Id = c.Id,
        Name = c.Name,
        NameEn = c.NameEn,
        Alpha2 = c.Alpha2,
        Alpha3 = c.Alpha3,
        Numeric = c.Numeric,
        VisaRequired = c.VisaRequired,
        BiometricsRequired = c.BiometricsRequired,
        IsEu = c.IsEu,
        LanguageId = c.Alpha3 is "CZE" or "SVK" ? CzechLanguageId : EnglishLanguageId,
      });
    }

    await _db.SaveChangesAsync(ct);

    if (_logger.IsEnabled(LogLevel.Information))
    {
      _logger.LogInformation("Seeded {Count} nationalities", Countries.All.Count);
    }
  }
}
