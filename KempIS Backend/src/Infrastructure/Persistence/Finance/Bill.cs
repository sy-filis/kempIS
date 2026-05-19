using Domain.Finance.Bills;
using Domain.Finance.LegalEntities;
using Domain.Finance.Payers;
using Domain.Finance.Payments;
using Domain.Reservations.Reservations;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Finance;

internal sealed class BillConfiguration : IEntityTypeConfiguration<Bill>
{
  public void Configure(EntityTypeBuilder<Bill> builder)
  {
    builder.ToTable("Bills");

    builder.HasKey(b => b.Id);

    builder.Property(b => b.Number)
      .HasMaxLength(50)
      .IsRequired();

    builder.HasIndex(b => b.Number).IsUnique();

    builder.Property(b => b.IssuedAtUtc)
      .IsRequired();

    builder.Property(b => b.CheckInAt)
      .HasColumnType("date")
      .IsRequired();

    builder.Property(b => b.CheckOutAt)
      .HasColumnType("date")
      .IsRequired();

    builder.Property(b => b.DocumentContent)
      .HasColumnType("bytea");

    builder.Property(b => b.DocumentGeneratedAtUtc);

    builder.Property(b => b.Kind)
      .HasConversion<string>()
      .HasMaxLength(16)
      .IsRequired();

    builder.Property(b => b.OriginalBillId);

    builder.Property(b => b.RepairReason)
      .HasMaxLength(500);

    builder.HasOne<Bill>()
      .WithMany()
      .HasForeignKey(b => b.OriginalBillId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<Reservation>()
      .WithMany()
      .HasForeignKey(b => b.ReservationId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.Property(b => b.LanguageIdGuid)
      .IsRequired();

    builder.Property(b => b.FinancialClosingId);

    builder.Property(b => b.Scartation).HasColumnType("date");
    builder.HasIndex(b => b.Scartation).HasFilter("\"scartation\" IS NOT NULL");

    builder.OwnsOne(b => b.Payer, p =>
    {
      p.ConfigurePayer();
    });

    builder.OwnsOne(b => b.LegalEntity, le =>
    {
      le.Property(l => l.Name)
        .HasMaxLength(255)
        .IsRequired();

      le.OwnsOne(l => l.Address, address =>
      {
        address.ConfigureAddress();
      });

      le.Property(l => l.Cin)
        .HasMaxLength(255)
        .IsRequired();

      le.Property(l => l.Tin)
        .HasMaxLength(255);
    });
    builder.Navigation(b => b.LegalEntity).IsRequired(false);

    builder.OwnsOne(b => b.Payment, p =>
    {
      p.Property(l => l.Amount)
        .HasPrecision(18, 2)
        .IsRequired();

      p.Property(l => l.PaymentType)
        .HasConversion<string>()
        .HasMaxLength(16)
        .IsRequired();
    });
  }
}
