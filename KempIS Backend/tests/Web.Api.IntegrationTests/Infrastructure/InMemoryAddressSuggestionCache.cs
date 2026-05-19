using System.Collections.Concurrent;
using Application.Abstractions.Addresses;
using Application.Addresses.Queries.SuggestAddresses;

namespace Web.Api.IntegrationTests.Infrastructure;

public sealed class InMemoryAddressSuggestionCache : IAddressSuggestionCache
{
  private readonly ConcurrentDictionary<string, IReadOnlyList<AddressSuggestion>> _store = new();

  public Task<IReadOnlyList<AddressSuggestion>?> GetAsync(string key, CancellationToken cancellationToken) =>
    Task.FromResult(_store.TryGetValue(key, out IReadOnlyList<AddressSuggestion>? value) ? value : null);

  public Task SetAsync(string key, IReadOnlyList<AddressSuggestion> value, TimeSpan ttl, CancellationToken cancellationToken)
  {
    _store[key] = value;
    return Task.CompletedTask;
  }

  public void Clear() => _store.Clear();

  public void Seed(string key, IReadOnlyList<AddressSuggestion> value) => _store[key] = value;
}
