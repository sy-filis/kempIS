using Domain.Reservations.SpotGroups;
using Domain.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Reservations;

internal sealed class SpotGroupConfiguration : IEntityTypeConfiguration<SpotGroup>
{
  public void Configure(EntityTypeBuilder<SpotGroup> builder)
  {
    builder.ToTable("SpotGroups");

    builder.HasKey(sg => sg.Id);

    builder.Property(sg => sg.Name)
      .HasMaxLength(255)
      .IsRequired();

    builder.Property(sg => sg.Description)
      .HasMaxLength(1000);

    builder.Property(sg => sg.Capacity)
      .IsRequired();

    builder.Property(sg => sg.IsActive)
      .IsRequired();

    builder.Property(sg => sg.ImageUrl)
      .HasMaxLength(2048)
      .IsRequired();

    builder.Property(sg => sg.DetailsUrl)
      .HasMaxLength(2048)
      .IsRequired();

    builder.HasOne<Service>()
      .WithMany()
      .HasForeignKey(sg => sg.ServiceId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
