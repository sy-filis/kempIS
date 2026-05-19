using Domain.Services.ServiceTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Services;

internal sealed class ServiceTypeConfiguration : IEntityTypeConfiguration<ServiceType>
{
  public void Configure(EntityTypeBuilder<ServiceType> builder)
  {
    builder.ToTable("ServiceTypes");

    builder.HasKey(st => st.Id);

    builder.Property(st => st.Name)
      .HasMaxLength(255)
      .IsRequired();

    builder.Property(st => st.IsActive)
      .IsRequired();
  }
}
