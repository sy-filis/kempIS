using Domain.Finance.Bills;
using Domain.Reservations.Reservations;
using Domain.Reservations.Vehicles;
using Domain.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Reservations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
  public void Configure(EntityTypeBuilder<Vehicle> builder)
  {
    builder.ToTable("Vehicles");

    builder.HasKey(v => v.Id);

    builder.Property(v => v.RegistrationNumber)
      .HasMaxLength(20)
      .IsRequired();

    builder.HasOne<Reservation>()
      .WithMany()
      .HasForeignKey(v => v.ReservationId)
      .IsRequired(false)
      .OnDelete(DeleteBehavior.SetNull);

    builder.HasOne<Bill>()
      .WithMany()
      .HasForeignKey(v => v.BillId)
      .IsRequired(false)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<Service>()
      .WithMany()
      .HasForeignKey(v => v.ServiceId)
      .IsRequired(false)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
