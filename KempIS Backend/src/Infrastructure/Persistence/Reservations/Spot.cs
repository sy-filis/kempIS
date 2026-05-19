using Domain.Reservations.SpotGroups;
using Domain.Reservations.Spots;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Reservations;

internal sealed class SpotConfiguration : IEntityTypeConfiguration<Spot>
{
  public void Configure(EntityTypeBuilder<Spot> builder)
  {
    builder.ToTable("Spots");

    builder.HasKey(s => s.Id);

    builder.Property(s => s.Name)
      .HasMaxLength(255)
      .IsRequired();

    builder.Property(s => s.Description)
      .HasMaxLength(1000);

    builder.Property(s => s.IsActive)
      .IsRequired();

    builder.HasOne<SpotGroup>()
      .WithMany()
      .HasForeignKey(s => s.SpotGroupId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasIndex(s => new { s.SpotGroupId, s.Name })
      .IsUnique();
  }
}
