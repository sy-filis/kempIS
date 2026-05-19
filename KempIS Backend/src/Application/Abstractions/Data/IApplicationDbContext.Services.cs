using Domain.Services.Languages;
using Domain.Services.Services;
using Domain.Services.ServiceTexts;
using Domain.Services.ServiceTypes;
using Domain.Services.VatRates;
using Microsoft.EntityFrameworkCore;

namespace Application.Abstractions.Data;

public partial interface IApplicationDbContext
{
  DbSet<Language> Languages { get; }
  DbSet<Service> Services { get; }
  DbSet<ServiceText> ServiceTexts { get; }
  DbSet<ServiceType> ServiceTypes { get; }
  DbSet<VatRate> VatRates { get; }
}
