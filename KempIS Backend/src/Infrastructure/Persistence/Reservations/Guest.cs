using Domain.Finance.Bills;
using Domain.Reservations.Guests;
using Domain.Reservations.Reservations;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Reservations;

internal sealed class GuestConfiguration : IEntityTypeConfiguration<Guest>
{
  public void Configure(EntityTypeBuilder<Guest> builder)
  {
    builder.ToTable("Guests");

    builder.HasKey(g => g.Id);

    builder.Property(g => g.FirstName)
      .HasMaxLength(255)
      .IsRequired();

    builder.Property(g => g.LastName)
      .HasMaxLength(255)
      .IsRequired();

    builder.Property(g => g.DateOfBirth)
      .IsRequired();

    builder.Property(g => g.DocumentNumber)
      .HasMaxLength(50);

    builder.Property(g => g.DocumentType)
      .HasConversion<int?>();

    builder.Property(g => g.CreatedAt).IsRequired();
    builder.Property(g => g.UpdatedAt).IsRequired();
    builder.Property(g => g.ReportedAt);

    builder.Property(g => g.ReasonOfStay)
      .HasMaxLength(500)
      .IsRequired();

    builder.ComplexProperty(g => g.StayDateRange, dr =>
    {
      dr.IsRequired(false);
      dr.Property(d => d.From).HasColumnName("DateRangeFrom");
      dr.Property(d => d.To).HasColumnName("DateRangeTo");
    });

    builder.Property(g => g.VisaNumber)
      .HasMaxLength(50);

    builder.Property(g => g.Note)
      .HasMaxLength(1000);

    builder.Property(g => g.Scartation);
    builder.HasIndex(g => g.Scartation).HasFilter("\"scartation\" IS NOT NULL");
    builder.Property(g => g.CheckInAt);
    builder.Property(g => g.CheckOutAt);

    builder.Property(g => g.SignaturePng)
      .HasColumnType("bytea")
      .HasMaxLength(1_048_576);

    builder.Property(g => g.SignatureCapturedAtUtc);

    builder.OwnsOne(g => g.Address, address =>
    {
      address.ConfigureAddress();
    });

    builder.HasOne<Reservation>()
      .WithMany()
      .HasForeignKey(g => g.ReservationId)
      .OnDelete(DeleteBehavior.SetNull);

    builder.HasOne(g => g.Nationality)
      .WithMany()
      .HasForeignKey(g => g.NationalityId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<Bill>()
      .WithMany()
      .HasForeignKey(g => g.BillId)
      .OnDelete(DeleteBehavior.SetNull);
  }
}
