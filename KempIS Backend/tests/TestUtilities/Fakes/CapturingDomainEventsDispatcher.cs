using System.Collections.Concurrent;
using Infrastructure.DomainEvents;
using SharedKernel;

namespace TestUtilities.Fakes;

public sealed class CapturingDomainEventsDispatcher : IDomainEventsDispatcher
{
  private readonly ConcurrentQueue<IDomainEvent> _dispatched = new();

  public IReadOnlyCollection<IDomainEvent> Dispatched => _dispatched.ToArray();

  public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
  {
    foreach (IDomainEvent domainEvent in domainEvents)
    {
      _dispatched.Enqueue(domainEvent);
    }
    return Task.CompletedTask;
  }
}
