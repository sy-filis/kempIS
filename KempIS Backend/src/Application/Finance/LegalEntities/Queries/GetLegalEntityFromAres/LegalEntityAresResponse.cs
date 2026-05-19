namespace Application.Finance.LegalEntities.Queries.GetLegalEntityFromAres;

public sealed record LegalEntityFinderResponse(
    string Name,
    string Cin,
    string? Tin,
    AresAddressResponse Address);

public sealed record AresAddressResponse(
    string CountryCode,
    string City,
    string ZipCode,
    string? Street,
    string HouseNumber);
