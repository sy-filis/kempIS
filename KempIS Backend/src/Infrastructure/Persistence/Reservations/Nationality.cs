using Domain.Reservations.Nationalities;
using Domain.Services.Languages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Reservations;

internal sealed class NationalityConfiguration : IEntityTypeConfiguration<Nationality>
{
  public void Configure(EntityTypeBuilder<Nationality> builder)
  {
    builder.ToTable("Nationalities");

    builder.HasKey(n => n.Id);

    builder.Property(n => n.Name)
      .HasMaxLength(100)
      .IsRequired();

    builder.Property(n => n.NameEn)
      .HasMaxLength(100)
      .IsRequired();

    builder.Property(n => n.Alpha2)
      .HasMaxLength(2)
      .IsRequired();

    builder.Property(n => n.Alpha3)
      .HasMaxLength(3)
      .IsRequired();

    builder.Property(n => n.Numeric)
      .HasMaxLength(3)
      .IsRequired();

    builder.Property(n => n.VisaRequired)
      .IsRequired();

    builder.Property(n => n.BiometricsRequired)
      .IsRequired();

    builder.Property(n => n.IsEu)
      .IsRequired();

    builder.HasIndex(n => n.Alpha2).IsUnique();
    builder.HasIndex(n => n.Alpha3).IsUnique();
    builder.HasIndex(n => n.Numeric).IsUnique();

    builder.HasOne<Language>()
      .WithMany()
      .HasForeignKey(n => n.LanguageId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
