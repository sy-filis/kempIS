using Domain.Operations.CleanInfos;
using Domain.Operations.CleaningPlans;
using Domain.Reservations.Spots;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Operations;

internal sealed class CleanInfoConfiguration : IEntityTypeConfiguration<CleanInfo>
{
  public void Configure(EntityTypeBuilder<CleanInfo> builder)
  {
    builder.ToTable("CleanInfos");

    builder.HasKey(ci => ci.Id);

    builder.Property(ci => ci.ResponsibleUserId);

    builder.Property(ci => ci.CompletedAtUtc);

    builder.Property(ci => ci.Note)
      .HasMaxLength(500);

    builder.HasIndex(c => new { c.CleaningPlanId, c.SpotId }).IsUnique();

    builder.HasOne<CleaningPlan>()
      .WithMany()
      .HasForeignKey(ci => ci.CleaningPlanId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne<Spot>()
      .WithMany()
      .HasForeignKey(ci => ci.SpotId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
