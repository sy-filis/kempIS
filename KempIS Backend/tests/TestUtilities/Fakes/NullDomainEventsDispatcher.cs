using Infrastructure.DomainEvents;
using SharedKernel;

namespace TestUtilities.Fakes;

/// <summary>
/// No-op dispatcher. Tests asserting domain events must snapshot them on the entity
/// before SaveChangesAsync (or use <see cref="CapturingDomainEventsDispatcher"/>).
/// </summary>
public sealed class NullDomainEventsDispatcher : IDomainEventsDispatcher
{
  public static readonly NullDomainEventsDispatcher Instance = new();

  public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
      => Task.CompletedTask;
}
