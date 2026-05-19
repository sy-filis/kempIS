using Infrastructure.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Reservations;

internal sealed class ReservationNumberSequenceConfiguration
  : IEntityTypeConfiguration<ReservationNumberSequence>
{
  public void Configure(EntityTypeBuilder<ReservationNumberSequence> builder)
  {
    builder.ToTable("ReservationNumberSequences");
    builder.HasKey(s => s.Year);
    builder.Property(s => s.Year).ValueGeneratedNever();
    builder.Property(s => s.LastSeq).IsRequired();
  }
}
