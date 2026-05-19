using Domain.Finance.InvoiceItems;
using Domain.Finance.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Finance;

internal sealed class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
  public void Configure(EntityTypeBuilder<InvoiceItem> builder)
  {
    builder.ToTable("InvoiceItems");

    builder.HasKey(ii => ii.Id);

    builder.Property(ii => ii.Quantity)
      .HasPrecision(18, 4)
      .IsRequired();

    builder.Property(ii => ii.UnitPrice)
      .HasPrecision(18, 2)
      .IsRequired();

    builder.Property(ii => ii.VatRatePercentage)
      .HasPrecision(5, 2)
      .IsRequired();

    builder.Property(ii => ii.ServiceGuid)
      .IsRequired();

    builder.HasOne<Invoice>()
      .WithMany()
      .HasForeignKey(ii => ii.InvoiceId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasIndex(ii => ii.InvoiceId);
  }
}
