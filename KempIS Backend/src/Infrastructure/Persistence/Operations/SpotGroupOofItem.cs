using Domain.Operations.OutOfOrders;
using Domain.Operations.SpotGroupOOFItems;
using Domain.Reservations.SpotGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Operations;

internal sealed class SpotGroupOofItemConfiguration : IEntityTypeConfiguration<SpotGroupOofItem>
{
  public void Configure(EntityTypeBuilder<SpotGroupOofItem> builder)
  {
    builder.ToTable("SpotGroupOofItems");

    builder.HasKey(sg => sg.Id);

    builder.HasOne<OutOfOrder>()
      .WithMany(o => o.SpotGroupItems)
      .HasForeignKey(sg => sg.OutOfOrderId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne<SpotGroup>()
      .WithMany()
      .HasForeignKey(sg => sg.SpotGroupId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
