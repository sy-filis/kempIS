using Infrastructure.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Reservations;

internal sealed class GroupReservationNumberSequenceConfiguration
  : IEntityTypeConfiguration<GroupReservationNumberSequence>
{
  public void Configure(EntityTypeBuilder<GroupReservationNumberSequence> builder)
  {
    builder.ToTable("GroupReservationNumberSequences");
    builder.HasKey(s => s.Year);
    builder.Property(s => s.Year).ValueGeneratedNever();
    builder.Property(s => s.LastSeq).IsRequired();
  }
}
