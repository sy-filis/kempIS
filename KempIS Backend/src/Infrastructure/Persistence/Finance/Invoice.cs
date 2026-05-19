using Domain.Finance.Bills;
using Domain.Finance.Invoices;
using Domain.Reservations.Reservations;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Finance;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
  public void Configure(EntityTypeBuilder<Invoice> builder)
  {
    builder.ToTable("Invoices");

    builder.HasKey(i => i.Id);

    builder.Property(i => i.Number)
      .HasMaxLength(50);

    // Filtered unique index: enforced only when Number is not null.
    builder.HasIndex(i => i.Number)
      .IsUnique()
      .HasFilter("\"number\" IS NOT NULL");

    builder.Property(i => i.Status)
      .HasConversion<string>()
      .HasMaxLength(16)
      .IsRequired();

    builder.Property(i => i.IssuedAt)
      .HasColumnType("date")
      .IsRequired();

    builder.Property(i => i.PaidAt).HasColumnType("date");

    builder.Property(i => i.DueTo).HasColumnType("date");

    builder.Property(i => i.Email)
      .HasMaxLength(320)
      .IsRequired();

    builder.Property(i => i.PhoneNumber)
      .HasMaxLength(32)
      .IsRequired();

    builder.HasOne<Reservation>()
      .WithMany()
      .HasForeignKey(i => i.ReservationId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.Property(i => i.ReservationId)
      .IsRequired();

    builder.HasOne<Bill>()
      .WithMany()
      .HasForeignKey(i => i.LinkedBillId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.Property(i => i.LinkedBillId);

    builder.Property(i => i.Scartation).HasColumnType("date");
    builder.HasIndex(i => i.Scartation).HasFilter("\"scartation\" IS NOT NULL");

    builder.OwnsOne(i => i.Payer, p =>
    {
      p.ConfigurePayer();
    });
    builder.Navigation(i => i.Payer).IsRequired(false);

    builder.OwnsOne(i => i.LegalEntity, le =>
    {
      le.Property(l => l.Name).HasMaxLength(255).IsRequired();
      le.OwnsOne(l => l.Address, address => { address.ConfigureAddress(); });
      le.Property(l => l.Cin).HasMaxLength(255).IsRequired();
      le.Property(l => l.Tin).HasMaxLength(255);
    });
    builder.Navigation(i => i.LegalEntity).IsRequired(false);
  }
}
