using Domain.Common;
using Domain.Finance.Payers;
using Infrastructure.Persistence.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel;

namespace Infrastructure.Persistence.Finance;

public static class PayerMappingExtensions
{
  public static void ConfigurePayer<TEntity>(
    this OwnedNavigationBuilder<TEntity, Payer> builder)
    where TEntity : class
  {
    builder.Property(p => p.Name)
      .HasMaxLength(255)
      .IsRequired();

    builder.Property(p => p.Surname)
      .HasMaxLength(255)
      .IsRequired();

    builder.OwnsOne(p => p.Address, address =>
    {
      address.ConfigureAddress();
    });
  }
}
