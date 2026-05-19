using System.Linq.Expressions;
using Domain.Common;
using Domain.Reservations.Nationalities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Common;

public static class AddressMappingExtension
{
  public static void ConfigureAddress<TEntity>(
    this OwnedNavigationBuilder<TEntity, Address> builder)
    where TEntity : class
  {
    builder.Property(x => x.CountryId)
      .IsRequired();

    builder.Property(x => x.City)
      .HasMaxLength(256)
      .IsRequired();

    builder.Property(x => x.ZipCode)
      .HasMaxLength(16)
      .IsRequired();

    builder.Property(x => x.Street)
      .HasMaxLength(256)
      .IsRequired();

    builder.Property(x => x.HouseNumber)
      .HasMaxLength(16)
      .IsRequired();

    builder.HasOne<Nationality>()
      .WithMany()
      .HasForeignKey(x => x.CountryId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
