namespace Application.Addresses.Queries.SuggestAddresses;

public sealed record AddressSuggestion(
  string CountryCode,
  string City,
  string ZipCode,
  string Street,
  string HouseNumber);
