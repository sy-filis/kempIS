using Domain.Finance.BillItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Finance;

internal sealed class BillItemConfiguration : IEntityTypeConfiguration<BillItem>
{
  public void Configure(EntityTypeBuilder<BillItem> builder)
  {
    builder.ToTable("BillItems");

    builder.HasKey(bi => bi.Id);

    builder.Property(bi => bi.UnitPrice)
      .HasPrecision(18, 2)
      .IsRequired();

    builder.Property(bi => bi.VatRatePercentage)
      .HasPrecision(5, 2)
      .IsRequired();

    builder.Property(bi => bi.Quantity)
      .IsRequired();

    builder.Property(bi => bi.RecapSingleQuantity)
      .IsRequired();

    builder.Property(bi => bi.RecapDayQuantity)
      .IsRequired();

    builder.HasOne<Domain.Finance.Bills.Bill>()
      .WithMany()
      .HasForeignKey(bi => bi.BillId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasIndex(bi => bi.BillId);
  }
}
