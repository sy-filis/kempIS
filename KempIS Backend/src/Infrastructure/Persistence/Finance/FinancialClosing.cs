using Domain.Finance.FinancialClosings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Finance;

internal sealed class FinancialClosingConfiguration : IEntityTypeConfiguration<FinancialClosing>
{
  public void Configure(EntityTypeBuilder<FinancialClosing> builder)
  {
    builder.ToTable("FinancialClosings");

    builder.HasKey(fc => fc.Id);

    builder.Property(fc => fc.ClosedAtUtc)
      .IsRequired();

    builder.Property(fc => fc.FinancialClosingId)
      .IsRequired();

    builder.HasIndex(fc => fc.FinancialClosingId)
      .IsUnique();

    builder.Property(fc => fc.TotalAmount)
      .HasPrecision(18, 2)
      .IsRequired();

    builder.Property(c => c.DocumentContent)
      .HasColumnType("bytea");

    builder.Property(c => c.DocumentGeneratedAtUtc);

    builder.Property(c => c.CreatedByUserId);
  }
}
