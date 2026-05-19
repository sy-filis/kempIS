using Domain.Finance.Bills;
using Domain.Reservations.Reservations;
using Domain.Reservations.ReservationSpotItems;
using Domain.Reservations.SpotGroups;
using Domain.Reservations.Spots;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Reservations;

internal sealed class ReservationSpotItemConfiguration : IEntityTypeConfiguration<ReservationSpotItem>
{
  public void Configure(EntityTypeBuilder<ReservationSpotItem> builder)
  {
    builder.ToTable("ReservationSpotItems");

    builder.HasKey(rsi => rsi.Id);

    builder.Property(rsi => rsi.HasGivenKey)
      .IsRequired();

    builder.Property(rsi => rsi.HasReturnedKeys)
      .IsRequired();

    builder.HasOne<Reservation>()
      .WithMany()
      .HasForeignKey(rsi => rsi.ReservationId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne<SpotGroup>()
      .WithMany()
      .HasForeignKey(rsi => rsi.SpotGroupId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<Spot>()
      .WithMany()
      .HasForeignKey(rsi => rsi.SpotId)
      .OnDelete(DeleteBehavior.Restrict)
      .IsRequired(false);

    builder.HasOne<Bill>()
      .WithMany()
      .HasForeignKey(rsi => rsi.BillId)
      .IsRequired(false)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasIndex(rsi => new { rsi.ReservationId, rsi.SpotId });
    builder.HasIndex(rsi => new { rsi.SpotGroupId, rsi.ReservationId });
  }
}
