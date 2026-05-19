using Application.Abstractions.Data;
using Domain.Reservations.Guests;
using Infrastructure.DomainEvents;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Npgsql;
using SharedKernel;

namespace Infrastructure.Database;

public sealed partial class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IDomainEventsDispatcher domainEventsDispatcher,
    IDateTimeProvider dateTimeProvider)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IApplicationDbContext
{
  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);

    builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

    builder.HasDefaultSchema(Schemas.Default);
  }

  public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    StampGuestTimestamps();

    int result;
    try
    {
      result = await base.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException ex)
        when (ex.InnerException is PostgresException { SqlState: "23503" or "23505" } pg)
    {
      ConstraintKind kind = pg.SqlState == "23503"
        ? ConstraintKind.ForeignKey
        : ConstraintKind.Unique;
      throw new DatabaseConstraintViolationException(kind, pg.ConstraintName, pg.Detail, ex);
    }

    await PublishDomainEventsAsync();

    return result;
  }

  private void StampGuestTimestamps()
  {
    DateTime now = dateTimeProvider.UtcNow;
    foreach (EntityEntry<Guest> entry in ChangeTracker.Entries<Guest>())
    {
      switch (entry.State)
      {
        case EntityState.Added:
          entry.Entity.CreatedAt = now;
          entry.Entity.UpdatedAt = now;
          break;
        case EntityState.Modified:
          entry.Entity.UpdatedAt = now;
          break;
      }
    }
  }

  private async Task PublishDomainEventsAsync()
  {
    var domainEvents = ChangeTracker
      .Entries<Entity>()
      .Select(entry => entry.Entity)
      .SelectMany(entity =>
      {
        List<IDomainEvent> domainEvents = entity.DomainEvents;

        entity.ClearDomainEvents();

        return domainEvents;
      })
      .ToList();

    await domainEventsDispatcher.DispatchAsync(domainEvents);
  }
}
