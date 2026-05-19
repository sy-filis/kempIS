using Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Common;

public static class DateRangeBuilderExtension
{
  public static ComplexPropertyBuilder<DateRange> ConfigureDateRange(
    this ComplexPropertyBuilder<DateRange> builder)
  {
    builder.Property(d => d.From)
      .HasColumnName("DateRangeFrom")
      .IsRequired();

    builder.Property(d => d.To)
      .HasColumnName("DateRangeTo")
      .IsRequired();

    return builder;
  }
}
