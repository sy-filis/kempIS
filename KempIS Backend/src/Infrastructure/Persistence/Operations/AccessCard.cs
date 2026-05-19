using Domain.Finance.Bills;
using Domain.Operations.AccessCards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Operations;

internal sealed class AccessCardConfiguration : IEntityTypeConfiguration<AccessCard>
{
  public void Configure(EntityTypeBuilder<AccessCard> builder)
  {
    builder.ToTable("AccessCards");

    builder.HasKey(ac => ac.Id);

    builder.Property(ac => ac.Uid)
      .IsRequired();

    builder.HasIndex(ac => ac.Uid)
      .IsUnique();

    builder.Property(ac => ac.Deposit)
      .HasPrecision(18, 2)
      .IsRequired();

    builder.Property(ac => ac.ValidUntil)
      .HasColumnType("date")
      .IsRequired();

    builder.Property(ac => ac.IssuedAtUtc)
      .IsRequired();

    builder.Property(ac => ac.Note);

    builder.HasOne<Bill>()
      .WithMany()
      .HasForeignKey(ac => ac.BillId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
