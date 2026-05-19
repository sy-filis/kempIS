using Domain.Reservations.SpotGroups;
using Domain.Reservations.Spots;
using Domain.Services.Services;
using Domain.Services.ServiceTypes;
using Domain.Services.VatRates;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Seed;

internal sealed class TestDataSeeder
{
  private static readonly Guid VatRate21Id = new("00a61443-6464-4de8-9eac-879053773415");

  private static readonly Guid AccommodationServiceTypeId = new("bb9fffe6-c270-455d-bc42-0bd54dd8a17e");
  private static readonly Guid PersonsServiceTypeId = new("5a6c3baa-f7e4-44ca-8600-55242c3e7cdf");
  private static readonly Guid MealsServiceTypeId = new("f7493309-01eb-4f1d-836a-837c56c898e6");
  private static readonly Guid RecreationFeeServiceTypeId = new("55bc9eb6-00d7-4a3d-8cba-b240fec91410");

  // Pinned by the frontend for prefill flows; do not regenerate.
  private static readonly Guid AdultServiceId = new("165cdc19-4347-4122-a58f-6d8ba172dfb7");
  private static readonly Guid ChildServiceId = new("61737953-5677-40f1-be1b-8264d65b8b87");
  private static readonly Guid BreakfastServiceId = new("0bc2e7de-83c1-4023-b32f-1ea0bcfb359d");
  private static readonly Guid LunchServiceId = new("f2e4ec18-c916-42fb-b0e8-912523b4e4f1");
  private static readonly Guid LunchPackageServiceId = new("4d7c9b1a-2e8f-4a3d-9b15-7f6c2a8e1d92");
  private static readonly Guid DinnerServiceId = new("086a1440-6c30-4997-aee3-242d5157970d");
  private static readonly Guid RecreationFeeServiceId = new("0f939342-cd59-4a4d-a9c8-22e06dee8b1a");

  private static readonly Guid BungalovServiceId = new("12828b6e-e02f-4e31-af7b-dd8048c0e5a9");
  private static readonly Guid StoneCottageServiceId = new("50d3b7a2-3fea-465d-a0c2-c4507ce72238");
  private static readonly Guid BungalovySpotGroupId = new("1de916bc-5613-4df6-a9dc-06f423927f18");
  private static readonly Guid StoneCottagesSpotGroupId = new("de0f2387-cc93-46bd-b244-0c7ed762773b");

  private readonly ApplicationDbContext _db;
  private readonly ILogger<TestDataSeeder> _logger;

  public TestDataSeeder(ApplicationDbContext db, ILogger<TestDataSeeder> logger)
  {
    _db = db;
    _logger = logger;
  }

  public async Task SeedAsync(CancellationToken cancellationToken)
  {
    int inserted = 0;

    inserted += await EnsureVatRateAsync(cancellationToken);
    inserted += await EnsureServiceTypesAsync(cancellationToken);
    inserted += await EnsureServicesAsync(cancellationToken);
    inserted += await EnsureSpotGroupsAsync(cancellationToken);
    inserted += await EnsureSpotsAsync(cancellationToken);

    if (inserted > 0)
    {
      await _db.SaveChangesAsync(cancellationToken);
      if (_logger.IsEnabled(LogLevel.Information))
      {
        _logger.LogInformation("Seeded {Count} test-data rows", inserted);
      }
    }
    else
    {
      _logger.LogDebug("Test data already present; nothing to insert");
    }
  }

  private async Task<int> EnsureVatRateAsync(CancellationToken ct)
  {
    if (await _db.VatRates.AnyAsync(v => v.Id == VatRate21Id, ct))
    {
      return 0;
    }

    _db.VatRates.Add(new VatRate
    {
      Id = VatRate21Id,
      Name = "DPH 21%",
      Rate = 21.00m,
      IsActive = true,
    });
    return 1;
  }

  private async Task<int> EnsureServiceTypesAsync(CancellationToken ct)
  {
    (Guid id, string name)[] types =
    [
      (AccommodationServiceTypeId, "Ubytování"),
      (PersonsServiceTypeId, "Osoby"),
      (MealsServiceTypeId, "Stravování"),
      (RecreationFeeServiceTypeId, "Rekreační poplatek"),
    ];

    var existingIds = (await _db.ServiceTypes
        .Where(t => types.Select(x => x.id).Contains(t.Id))
        .Select(t => t.Id)
        .ToListAsync(ct))
      .ToHashSet();

    int added = 0;
    foreach ((Guid id, string name) in types)
    {
      if (existingIds.Contains(id))
      {
        continue;
      }

      _db.ServiceTypes.Add(new ServiceType
      {
        Id = id,
        Name = name,
        IsActive = true,
      });
      added++;
    }

    return added;
  }

  private async Task<int> EnsureServicesAsync(CancellationToken ct)
  {
    (Guid id, ServiceGroup group, Guid typeId, string name, decimal basePrice)[] services =
    [
      (AdultServiceId, ServiceGroup.Persons, PersonsServiceTypeId, "Adult", 0m),
      (ChildServiceId, ServiceGroup.Persons, PersonsServiceTypeId, "Child", 0m),
      (BreakfastServiceId, ServiceGroup.Meals, MealsServiceTypeId, "Breakfast", 0m),
      (LunchServiceId, ServiceGroup.Meals, MealsServiceTypeId, "Lunch", 0m),
      (LunchPackageServiceId, ServiceGroup.Meals, MealsServiceTypeId, "Obědový balíček", 0m),
      (DinnerServiceId, ServiceGroup.Meals, MealsServiceTypeId, "Dinner", 0m),
      (RecreationFeeServiceId, ServiceGroup.RecreationFees, RecreationFeeServiceTypeId, "Recreation fee", 0m),

      (BungalovServiceId, ServiceGroup.Spots, AccommodationServiceTypeId, "Bungalov", 1000.00m),
      (StoneCottageServiceId, ServiceGroup.Spots, AccommodationServiceTypeId, "Zděné chaty", 800.00m),
    ];

    var existingIds = (await _db.Services
        .Where(s => services.Select(x => x.id).Contains(s.Id))
        .Select(s => s.Id)
        .ToListAsync(ct))
      .ToHashSet();

    int added = 0;
    foreach ((Guid id, ServiceGroup group, Guid typeId, string name, decimal basePrice) in services)
    {
      if (existingIds.Contains(id))
      {
        continue;
      }

      _db.Services.Add(new Service
      {
        Id = id,
        ServiceGroup = group,
        ServiceTypeId = typeId,
        VatRateId = VatRate21Id,
        Name = name,
        BasePrice = basePrice,
        IsActive = true,
      });
      added++;
    }

    return added;
  }

  private async Task<int> EnsureSpotGroupsAsync(CancellationToken ct)
  {
    int added = 0;

    if (!await _db.SpotGroups.AnyAsync(sg => sg.Id == BungalovySpotGroupId, ct))
    {
      _db.SpotGroups.Add(new SpotGroup
      {
        Id = BungalovySpotGroupId,
        ServiceId = StoneCottageServiceId,
        Name = "Bungalovy",
        Description = null,
        Capacity = 5,
        IsActive = true,
        ImageUrl = "https://www.olsovec.cz/files/ckeditor/Ubytovani/Bungalov/bungalov_hlavni.jpg",
        DetailsUrl = "https://www.olsovec.cz/bungalovy/",
      });
      added++;
    }

    if (!await _db.SpotGroups.AnyAsync(sg => sg.Id == StoneCottagesSpotGroupId, ct))
    {
      _db.SpotGroups.Add(new SpotGroup
      {
        Id = StoneCottagesSpotGroupId,
        ServiceId = BungalovServiceId,
        Name = "Zděné chaty",
        Description = null,
        Capacity = 5,
        IsActive = true,
        ImageUrl = "https://www.olsovec.cz/files/ckeditor/Ubytovani/SWC/SWC-hlavni.jpg",
        DetailsUrl = "https://www.olsovec.cz/zdene/",
      });
      added++;
    }

    return added;
  }

  private async Task<int> EnsureSpotsAsync(CancellationToken ct)
  {
    (Guid id, Guid spotGroupId, string name)[] spots =
    [
      (new Guid("a68816d2-e10a-45b8-953c-530bf03df53c"), BungalovySpotGroupId, "B1"),
      (new Guid("ee2359b0-145c-40d3-b02a-6265d0143673"), BungalovySpotGroupId, "B2"),
      (new Guid("5c0f4461-c189-4e2c-af15-a6e5a6d68210"), BungalovySpotGroupId, "B3"),
      (new Guid("0241eacf-018c-46c4-924a-6b162b33040f"), BungalovySpotGroupId, "B4"),
      (new Guid("0241eacf-018c-46c4-924a-6b162b33040e"), BungalovySpotGroupId, "B5"),
      (new Guid("cdb02cf4-f752-4c97-a1d3-3d167d31d0d5"), BungalovySpotGroupId, "B6"),
      (new Guid("35280ca3-6108-4eab-b9fa-965347e61af3"), BungalovySpotGroupId, "B7"),
      (new Guid("af81652d-72bc-4091-99a3-26e4c55ca00e"), BungalovySpotGroupId, "B8"),
      (new Guid("d783902a-b7ff-4045-92b6-ee590daf606a"), BungalovySpotGroupId, "B9"),
      (new Guid("bebf515d-2dba-481a-931d-03fbb5322b71"), BungalovySpotGroupId, "B10"),
      (new Guid("dc47c8bc-6f85-488e-b04e-d916cfa29ad0"), BungalovySpotGroupId, "B12"),
      (new Guid("4ac2f064-e369-437e-bc2f-a9d64e97c99b"), BungalovySpotGroupId, "B11"),
      (new Guid("2e8a9a0a-d8af-4e19-8ca1-ce56f0ebb21f"), BungalovySpotGroupId, "B13"),

      (new Guid("fb32aa17-963d-4975-ac48-408420a882b6"), StoneCottagesSpotGroupId, "SWC15"),
      (new Guid("30602852-7bf6-4e07-98e9-880afbaa7872"), StoneCottagesSpotGroupId, "SWC16"),
      (new Guid("c9c5ebb8-41f9-46da-b8a6-c5473180a2c3"), StoneCottagesSpotGroupId, "SWC17"),
      (new Guid("e5e9779d-d189-48c6-a383-97569f61d7fc"), StoneCottagesSpotGroupId, "SWC18"),
    ];

    var existingIds = (await _db.Spots
        .Where(s => spots.Select(x => x.id).Contains(s.Id))
        .Select(s => s.Id)
        .ToListAsync(ct))
      .ToHashSet();

    int added = 0;
    foreach ((Guid id, Guid spotGroupId, string name) in spots)
    {
      if (existingIds.Contains(id))
      {
        continue;
      }

      _db.Spots.Add(new Spot
      {
        Id = id,
        SpotGroupId = spotGroupId,
        Name = name,
        Description = null,
        IsActive = true,
      });
      added++;
    }

    return added;
  }
}
