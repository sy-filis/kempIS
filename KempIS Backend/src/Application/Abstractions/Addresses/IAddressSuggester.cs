using Application.Addresses.Queries.SuggestAddresses;
using SharedKernel;

namespace Application.Abstractions.Addresses;

public interface IAddressSuggester
{
  Task<Result<IReadOnlyList<AddressSuggestion>>> SuggestAsync(
    string query,
    int limit,
    CancellationToken cancellationToken);
}
