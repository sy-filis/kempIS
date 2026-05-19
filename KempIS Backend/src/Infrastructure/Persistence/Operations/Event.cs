using Domain.Operations.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Operations;

internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
  public void Configure(EntityTypeBuilder<Event> builder)
  {
    builder.ToTable("Events");

    builder.HasKey(e => e.Id);

    builder.Property(e => e.Name)
      .HasMaxLength(255)
      .IsRequired();

    builder.Property(e => e.Description)
      .HasMaxLength(1000);

    builder.Property(e => e.StartsAt)
      .IsRequired();

    builder.Property(e => e.EndsAt);
  }
}
