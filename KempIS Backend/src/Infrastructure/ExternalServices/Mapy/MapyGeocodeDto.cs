using System.Text.Json.Serialization;

namespace Infrastructure.ExternalServices.Mapy;

internal sealed record MapyGeocodeDto(
  [property: JsonPropertyName("items")] IReadOnlyList<MapyItem>? Items);

internal sealed record MapyItem(
  [property: JsonPropertyName("type")] string? Type,
  [property: JsonPropertyName("location")] string? Location,
  [property: JsonPropertyName("zip")] string? Zip,
  [property: JsonPropertyName("regionalStructure")] IReadOnlyList<MapyRegionalPart>? RegionalStructure);

internal sealed record MapyRegionalPart(
  [property: JsonPropertyName("name")] string? Name,
  [property: JsonPropertyName("type")] string? Type,
  [property: JsonPropertyName("isoCode")] string? IsoCode);
