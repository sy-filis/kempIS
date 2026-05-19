using Domain.Reservations.GroupReservations;
using Domain.Reservations.ReservationMakers;
using Domain.Reservations.Reservations;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Domain.Reservations.Reservations.OnlineCheckInStatus;

namespace Infrastructure.Persistence.Reservations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
  public void Configure(EntityTypeBuilder<Reservation> builder)
  {
    builder.ToTable("Reservations");

    builder.HasKey(r => r.Id);

    builder.Property(r => r.Number)
      .HasMaxLength(32)
      .IsRequired();

    builder.HasIndex(r => r.Number)
      .IsUnique();

    builder.ComplexProperty(r => r.Period, p => p.ConfigureDateRange());

    builder.Property(r => r.State)
      .HasConversion<string>()
      .IsRequired();

    builder.Property(r => r.CreatedAtUtc)
      .IsRequired();

    builder.Property(r => r.UpdatedAtUtc);

    builder.Property(r => r.Note)
      .HasMaxLength(1000);

    builder.Property(r => r.DisplayName)
      .HasMaxLength(100);

    builder.Property(r => r.Secret)
      .HasMaxLength(64)
      .IsRequired();

    builder.HasIndex(r => r.Secret);

    builder.Property(r => r.Language)
      .HasMaxLength(8)
      .HasDefaultValue(Domain.Reservations.ReservationLanguages.Czech)
      .IsRequired();

    builder.Property(r => r.GroupReservationId);

    builder.Property(r => r.OnlineCheckInStatus)
      .HasConversion<int>()
      .HasDefaultValue(OnlineCheckInStatus.None)
      .IsRequired();

    builder.ComplexProperty(r => r.ReservationMaker);

    builder.HasOne<GroupReservation>()
      .WithMany()
      .HasForeignKey(r => r.GroupReservationId)
      .OnDelete(DeleteBehavior.SetNull);
  }
}
