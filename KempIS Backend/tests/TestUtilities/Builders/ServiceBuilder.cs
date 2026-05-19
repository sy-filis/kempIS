using Domain.Services.Services;
using Domain.Services.ServiceTypes;
using Domain.Services.VatRates;

namespace TestUtilities.Builders;

public static class ServiceBuilder
{
  public static async Task<Guid> SeedAsync(Microsoft.EntityFrameworkCore.DbContext db)
  {
    var typeId = Guid.NewGuid();
    var vatId = Guid.NewGuid();
    var serviceId = Guid.NewGuid();

    db.Add(new ServiceType
    {
      Id = typeId,
      Name = "T",
      IsActive = true,
    });
    db.Add(new VatRate
    {
      Id = vatId,
      Name = "Zero",
      Rate = 0m,
      IsActive = true,
    });
    db.Add(new Service
    {
      Id = serviceId,
      Name = "S",
      ServiceGroup = ServiceGroup.Others,
      ServiceTypeId = typeId,
      VatRateId = vatId,
      BasePrice = 0m,
      IsActive = true,
    });

    await db.SaveChangesAsync();
    return serviceId;
  }
}
