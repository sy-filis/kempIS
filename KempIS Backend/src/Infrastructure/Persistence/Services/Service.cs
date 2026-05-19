using Domain.Services.Services;
using Domain.Services.ServiceTypes;
using Domain.Services.VatRates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Services;

internal sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
  public void Configure(EntityTypeBuilder<Service> builder)
  {
    builder.ToTable("Services");

    builder.HasKey(s => s.Id);

    builder.Property(s => s.Name)
      .HasMaxLength(255)
      .IsRequired();

    builder.Property(s => s.ServiceGroup)
      .HasConversion<string>()
      .HasMaxLength(32)
      .IsRequired();

    builder.Property(s => s.BasePrice)
      .HasPrecision(18, 2)
      .IsRequired();

    builder.Property(s => s.IsActive)
      .IsRequired();

    builder.HasOne<ServiceType>()
      .WithMany()
      .HasForeignKey(s => s.ServiceTypeId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<VatRate>()
      .WithMany()
      .HasForeignKey(s => s.VatRateId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
