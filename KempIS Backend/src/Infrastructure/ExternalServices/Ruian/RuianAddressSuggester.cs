using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Abstractions.Addresses;
using Application.Addresses.Queries.SuggestAddresses;
using Domain.Common;
using SharedKernel;

namespace Infrastructure.ExternalServices.Ruian;

internal sealed partial class RuianAddressSuggester(HttpClient httpClient)
  : IAddressSuggester
{
  [GeneratedRegex(@"^(?<street>.+?)\s+(?<houseNumber>\S+),\s+(?<zip>\d{5})\s+(?<city>.+)$")]
  private static partial Regex AddressRegex();

  public async Task<Result<IReadOnlyList<AddressSuggestion>>> SuggestAsync(
    string query,
    int limit,
    CancellationToken cancellationToken)
  {
    string encoded = Uri.EscapeDataString(query);
    var relative = new Uri(
      $"findAddressCandidates?SingleLine={encoded}&outFields=*&f=pjson",
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

    RuianFindAddressCandidatesDto? dto;
    try
    {
      dto = await response.Content.ReadFromJsonAsync<RuianFindAddressCandidatesDto>(cancellationToken);
    }
    catch (JsonException)
    {
      return Result.Failure<IReadOnlyList<AddressSuggestion>>(AddressErrors.ProviderUnavailable);
    }

    if (dto?.Candidates is null)
    {
      return Result.Success<IReadOnlyList<AddressSuggestion>>(Array.Empty<AddressSuggestion>());
    }

    List<AddressSuggestion> suggestions = [];
    foreach (RuianCandidate candidate in dto.Candidates)
    {
      if (candidate.Score < 90 || string.IsNullOrWhiteSpace(candidate.Address))
      {
        continue;
      }

      Match match = AddressRegex().Match(candidate.Address);
      if (!match.Success)
      {
        continue;
      }

      string city = match.Groups["city"].Value;
      string street = match.Groups["street"].Value;
      // RUIAN emits "č.p." for village addresses without a real street; substitute the municipality.
      if (string.Equals(street, "č.p.", StringComparison.OrdinalIgnoreCase))
      {
        street = city;
      }

      suggestions.Add(new AddressSuggestion(
        CountryCode: "CZ",
        City: city,
        ZipCode: match.Groups["zip"].Value,
        Street: street,
        HouseNumber: match.Groups["houseNumber"].Value));

      if (suggestions.Count >= limit)
      {
        break;
      }
    }

    return Result.Success<IReadOnlyList<AddressSuggestion>>(suggestions);
  }
}
