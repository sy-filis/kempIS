using Domain.Operations.CleaningPlans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Operations;

internal sealed class CleaningPlanConfiguration : IEntityTypeConfiguration<CleaningPlan>
{
  public void Configure(EntityTypeBuilder<CleaningPlan> builder)
  {
    builder.ToTable("CleaningPlans");

    builder.HasKey(cp => cp.Id);

    builder.Property(cp => cp.Date)
      .IsRequired();

    builder.Property(cp => cp.UpdatedAtUtc);

    builder.Property(cp => cp.UpdatedByUserId);

    builder.HasIndex(cp => cp.Date)
      .IsUnique();
  }
}
