using Domain.Services.Languages;
using Domain.Services.Services;
using Domain.Services.ServiceTexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Services;

internal sealed class ServiceTextConfiguration : IEntityTypeConfiguration<ServiceText>
{
  public void Configure(EntityTypeBuilder<ServiceText> builder)
  {
    builder.ToTable("ServiceTexts");

    builder.HasKey(st => st.Id);

    builder.Property(st => st.PrintText)
      .HasMaxLength(1000)
      .IsRequired();

    builder.HasOne<Service>()
      .WithMany()
      .HasForeignKey(st => st.ServiceId)
      .OnDelete(DeleteBehavior.Cascade);

    builder.HasOne<Language>()
      .WithMany()
      .HasForeignKey(st => st.LanguageId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasIndex(st => new { st.ServiceId, st.LanguageId })
      .IsUnique();
  }
}
