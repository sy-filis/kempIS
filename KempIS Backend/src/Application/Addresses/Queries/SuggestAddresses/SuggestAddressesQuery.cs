using Application.Abstractions.Messaging;

namespace Application.Addresses.Queries.SuggestAddresses;

public sealed record SuggestAddressesQuery(string Query, bool Foreign, int Limit)
  : IQuery<IReadOnlyList<AddressSuggestion>>;
