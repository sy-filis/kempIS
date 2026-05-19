using Domain.Reservations;
using Domain.Reservations.GroupReservations;
using Domain.Reservations.Spots;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Reservations;

internal sealed class GroupReservationConfiguration : IEntityTypeConfiguration<GroupReservation>
{
  public void Configure(EntityTypeBuilder<GroupReservation> builder)
  {
    builder.ToTable("GroupReservations");

    builder.HasKey(gr => gr.Id);

    builder.Property(gr => gr.Number)
      .HasMaxLength(32)
      .IsRequired();

    builder.HasIndex(gr => gr.Number)
      .IsUnique();

    builder.ComplexProperty(gr => gr.Period, p => p.ConfigureDateRange());

    builder.Property(gr => gr.State)
      .HasConversion<string>()
      .IsRequired();

    builder.Property(gr => gr.Secret)
      .HasMaxLength(128)
      .IsRequired();

    builder.Property(gr => gr.OrganizerName)
      .HasMaxLength(256)
      .IsRequired();

    builder.Property(gr => gr.OrganizerEmail)
      .HasMaxLength(256)
      .IsRequired();

    builder.Property(gr => gr.OrganizerPhone)
      .HasMaxLength(50)
      .IsRequired();

    builder.Property(gr => gr.CreatedAtUtc)
      .IsRequired();

    builder.Property(gr => gr.UpdatedAtUtc);

    builder.Property(gr => gr.Note)
      .HasMaxLength(1000);

    builder.Property(gr => gr.DisplayName)
      .HasMaxLength(100);

    builder.Property(gr => gr.Language)
      .HasMaxLength(8)
      .HasDefaultValue(ReservationLanguages.Czech)
      .IsRequired();

    builder.HasMany(gr => gr.HeldSpots)
      .WithOne()
      .HasForeignKey(s => s.GroupReservationId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}

internal sealed class GroupReservationSpotConfiguration : IEntityTypeConfiguration<GroupReservationSpot>
{
  public void Configure(EntityTypeBuilder<GroupReservationSpot> builder)
  {
    builder.ToTable("GroupReservationSpots");

    builder.HasKey(s => new { s.GroupReservationId, s.SpotId });

    builder.HasOne<Spot>()
      .WithMany()
      .HasForeignKey(s => s.SpotId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
