using Domain.Operations.Events;
using Domain.Reservations.SpotGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Operations;

internal sealed class EventSpotGroupItemConfiguration : IEntityTypeConfiguration<EventSpotGroupItem>
{
  public void Configure(EntityTypeBuilder<EventSpotGroupItem> builder)
  {
    builder.ToTable("EventSpotGroupItems");

    builder.HasKey(esg => esg.Id);

    builder.HasOne<Event>()
      .WithMany(e => e.SpotGroupItems)
      .HasForeignKey(esg => esg.EventId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne<SpotGroup>()
      .WithMany()
      .HasForeignKey(esg => esg.SpotGroupId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasIndex(esg => new { esg.EventId, esg.SpotGroupId })
      .IsUnique();
  }
}
