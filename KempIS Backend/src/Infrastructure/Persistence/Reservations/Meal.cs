using Domain.Reservations.Meals;
using Domain.Reservations.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Reservations;

internal sealed class MealConfiguration : IEntityTypeConfiguration<Meal>
{
  public void Configure(EntityTypeBuilder<Meal> builder)
  {
    builder.ToTable("Meals");

    builder.HasKey(m => new { m.ReservationId, m.Date });

    builder.Property(m => m.Date)
      .IsRequired();

    builder.OwnsOne(m => m.Breakfast, ConfigureMealAmount);
    builder.OwnsOne(m => m.Lunch, ConfigureMealAmount);
    builder.OwnsOne(m => m.LunchPackage, ConfigureMealAmount);
    builder.OwnsOne(m => m.Dinner, ConfigureMealAmount);

    builder.Navigation(m => m.Breakfast).IsRequired();
    builder.Navigation(m => m.Lunch).IsRequired();
    builder.Navigation(m => m.LunchPackage).IsRequired();
    builder.Navigation(m => m.Dinner).IsRequired();

    builder.HasOne<Reservation>()
      .WithMany()
      .HasForeignKey(m => m.ReservationId)
      .OnDelete(DeleteBehavior.Cascade);
  }

  private static void ConfigureMealAmount(OwnedNavigationBuilder<Meal, MealAmount> builder)
  {
    builder.Property(a => a.At);
    builder.Property(a => a.Normal).IsRequired();
    builder.Property(a => a.GlutenFree).IsRequired();
    builder.Property(a => a.LactoseFree).IsRequired();
    builder.Property(a => a.Vegetarian).IsRequired();
    builder.Property(a => a.GlutenFreeLactoseFree).IsRequired();
    builder.Property(a => a.GlutenFreeVegetarian).IsRequired();
    builder.Property(a => a.LactoseFreeVegetarian).IsRequired();
    builder.Property(a => a.GlutenFreeLactoseFreeVegetarian).IsRequired();
  }
}
