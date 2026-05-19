using Domain.Operations.MaintenanceIssues;
using Domain.Reservations.Spots;
using Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Operations;

internal sealed class MaintenanceIssueConfiguration : IEntityTypeConfiguration<MaintenanceIssue>
{
  public void Configure(EntityTypeBuilder<MaintenanceIssue> builder)
  {
    builder.HasKey(m => m.Id);
    builder.Property(m => m.ProblemDescription).IsRequired().HasMaxLength(2000);
    builder.Property(m => m.Note).HasMaxLength(2000);
    builder.Property(m => m.IssuedAtUtc).IsRequired();

    builder.HasOne<Spot>()
      .WithMany()
      .HasForeignKey(m => m.SpotId)
      .OnDelete(DeleteBehavior.SetNull);

    builder.HasOne<ApplicationUser>()
      .WithMany()
      .HasForeignKey(m => m.SolverUserId)
      .OnDelete(DeleteBehavior.SetNull);

    builder.HasIndex(m => m.SpotId);
    builder.HasIndex(m => m.ResolvedAtUtc);
  }
}
