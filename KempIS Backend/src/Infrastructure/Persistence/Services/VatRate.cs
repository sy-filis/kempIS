using Domain.Services.VatRates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Services;

internal sealed class VatRateConfiguration : IEntityTypeConfiguration<VatRate>
{
  public void Configure(EntityTypeBuilder<VatRate> builder)
  {
    builder.ToTable("VatRates");

    builder.HasKey(vr => vr.Id);

    builder.Property(vr => vr.Name)
      .HasMaxLength(100)
      .IsRequired();

    builder.Property(vr => vr.Rate)
      .HasPrecision(5, 2)
      .IsRequired();

    builder.Property(vr => vr.IsActive)
      .IsRequired();
  }
}
