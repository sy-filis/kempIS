namespace Domain.Common;

public sealed record Address(
  Guid CountryId,
  string City,
  string ZipCode,
  string Street,
  string HouseNumber);
