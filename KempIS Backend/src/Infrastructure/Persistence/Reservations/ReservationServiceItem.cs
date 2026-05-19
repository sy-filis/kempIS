using Domain.Reservations.Reservations;
using Domain.Reservations.ReservationServiceItems;
using Domain.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Reservations;

internal sealed class ReservationServiceItemConfiguration : IEntityTypeConfiguration<ReservationServiceItem>
{
  public void Configure(EntityTypeBuilder<ReservationServiceItem> builder)
  {
    builder.ToTable("ReservationServiceItems");

    builder.HasKey(rsi => rsi.Id);

    builder.Property(rsi => rsi.Quantity).IsRequired();
    builder.Property(rsi => rsi.RecapSingleQuantity).IsRequired();
    builder.Property(rsi => rsi.RecapDayQuantity).IsRequired();
    builder.Property(rsi => rsi.ServiceId).IsRequired();

    builder.HasOne<Reservation>()
      .WithMany()
      .HasForeignKey(rsi => rsi.ReservationId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne<Service>()
      .WithMany()
      .HasForeignKey(rsi => rsi.ServiceId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasIndex(rsi => rsi.ReservationId);
    builder.HasIndex(rsi => new { rsi.ReservationId, rsi.ServiceId }).IsUnique();
  }
}
