using Infrastructure.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Finance;

internal sealed class BillNumberSequenceConfiguration
  : IEntityTypeConfiguration<BillNumberSequence>
{
  public void Configure(EntityTypeBuilder<BillNumberSequence> builder)
  {
    builder.ToTable("BillNumberSequences");
    builder.HasKey(s => s.Year);
    builder.Property(s => s.Year).ValueGeneratedNever();
    builder.Property(s => s.LastSeq).IsRequired();
  }
}
