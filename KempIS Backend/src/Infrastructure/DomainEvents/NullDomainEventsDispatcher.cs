using SharedKernel;

namespace Infrastructure.DomainEvents;

// Used only by the design-time DbContext factory.
internal sealed class NullDomainEventsDispatcher : IDomainEventsDispatcher
{
  public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
      => Task.CompletedTask;
}
