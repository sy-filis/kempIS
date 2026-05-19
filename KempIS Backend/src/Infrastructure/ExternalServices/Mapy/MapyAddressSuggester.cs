using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Abstractions.Addresses;
using Application.Addresses.Queries.SuggestAddresses;
using Domain.Common;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.ExternalServices.Mapy;

internal sealed class MapyAddressSuggester(HttpClient httpClient, IOptions<MapyOptions> options)
  : IAddressSuggester
{
  private readonly string _apiKey = options.Value.ApiKey;

  public async Task<Result<IReadOnlyList<AddressSuggestion>>> SuggestAsync(
    string query,
    int limit,
    CancellationToken cancellationToken)
  {
    string encodedQuery = Uri.EscapeDataString(query);
    string encodedApiKey = Uri.EscapeDataString(_apiKey);
    var relative = new Uri(
      string.Create(
        CultureInfo.InvariantCulture,
        $"geocode?query={encodedQuery}&lang=cs&limit={limit}&apikey={encodedApiKey}"),
      UriKind.Relative);

    HttpResponseMessage response;
    try
    {
      response = await httpClient.GetAsync(relative, cancellationToken);
    }
    catch (HttpRequestException)
    {
      return Result.Failure<IReadOnlyList<AddressSuggestion>>(AddressErrors.ProviderUnavailable);
    }
    catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
      return Result.Failure<IReadOnlyList<AddressSuggestion>>(AddressErrors.ProviderUnavailable);
    }

    if (!response.IsSuccessStatusCode)
    {
      return Result.Failure<IReadOnlyList<AddressSuggestion>>(AddressErrors.ProviderUnavailable);
    }

    MapyGeocodeDto? dto;
    try
    {
      dto = await response.Content.ReadFromJsonAsync<MapyGeocodeDto>(cancellationToken);
    }
    catch (JsonException)
    {
      return Result.Failure<IReadOnlyList<AddressSuggestion>>(AddressErrors.ProviderUnavailable);
    }

    if (dto?.Items is null)
    {
      return Result.Success<IReadOnlyList<AddressSuggestion>>(Array.Empty<AddressSuggestion>());
    }

    List<AddressSuggestion> suggestions = [];
    foreach (MapyItem item in dto.Items)
    {
      if (item.Type != "regional.address")
      {
        continue;
      }

      suggestions.Add(MapItem(item));
    }

    return Result.Success<IReadOnlyList<AddressSuggestion>>(suggestions);
  }

  private static AddressSuggestion MapItem(MapyItem item)
  {
    string countryCode = FindPart(item, "regional.country")?.IsoCode ?? string.Empty;
    string street = FindPart(item, "regional.street")?.Name ?? string.Empty;
    string houseNumber = FindPart(item, "regional.address")?.Name ?? string.Empty;
    string city = FindPart(item, "regional.municipality")?.Name
      ?? LocationBeforeComma(item.Location)
      ?? string.Empty;
    string zipCode = StripWhitespace(item.Zip);

    return new AddressSuggestion(countryCode, city, zipCode, street, houseNumber);
  }

  private static MapyRegionalPart? FindPart(MapyItem item, string type) =>
    item.RegionalStructure?.FirstOrDefault(p => p.Type == type);

  private static string? LocationBeforeComma(string? location)
  {
    if (string.IsNullOrWhiteSpace(location))
    {
      return null;
    }

    int idx = location.IndexOf(',', StringComparison.Ordinal);
    return idx < 0 ? location : location[..idx];
  }

  private static string StripWhitespace(string? value) =>
    string.IsNullOrEmpty(value) ? string.Empty : new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray());
}
