using Domain.Operations.OutOfOrders;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Operations;

internal sealed class OutOfOrderConfiguration : IEntityTypeConfiguration<OutOfOrder>
{
  public void Configure(EntityTypeBuilder<OutOfOrder> builder)
  {
    builder.ToTable("OutOfOrders");

    builder.HasKey(o => o.Id);

    builder.ComplexProperty(o => o.Period, p => p.ConfigureDateRange());

    builder.Property(o => o.Reason)
      .HasMaxLength(1000)
      .IsRequired();

    builder.HasMany(o => o.SpotGroupItems)
      .WithOne()
      .HasForeignKey(sg => sg.OutOfOrderId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasMany(o => o.SpotItems)
      .WithOne()
      .HasForeignKey(s => s.OutOfOrderId)
      .OnDelete(DeleteBehavior.Cascade);
  }
}
