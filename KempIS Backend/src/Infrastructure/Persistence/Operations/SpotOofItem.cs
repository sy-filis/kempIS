using Domain.Operations.OutOfOrders;
using Domain.Operations.SpotOOFItems;
using Domain.Reservations.Spots;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Operations;

internal sealed class SpotOofItemConfiguration : IEntityTypeConfiguration<SpotOofItem>
{
  public void Configure(EntityTypeBuilder<SpotOofItem> builder)
  {
    builder.ToTable("SpotOofItems");

    builder.HasKey(s => s.Id);

    builder.HasOne<OutOfOrder>()
      .WithMany(o => o.SpotItems)
      .HasForeignKey(s => s.OutOfOrderId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne<Spot>()
      .WithMany()
      .HasForeignKey(s => s.SpotId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
