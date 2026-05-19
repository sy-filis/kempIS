using Application.Addresses.Queries.SuggestAddresses;

namespace Application.Abstractions.Addresses;

public interface IAddressSuggestionCache
{
  Task<IReadOnlyList<AddressSuggestion>?> GetAsync(
    string key,
    CancellationToken cancellationToken);

  Task SetAsync(
    string key,
    IReadOnlyList<AddressSuggestion> value,
    TimeSpan ttl,
    CancellationToken cancellationToken);
}
