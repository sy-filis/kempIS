using System.Text.RegularExpressions;
using Application.Abstractions.Addresses;
using Application.Abstractions.Messaging;
using Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Application.Addresses.Queries.SuggestAddresses;

internal sealed partial class SuggestAddressesQueryHandler(
  [FromKeyedServices(AddressProvider.Ruian)] IAddressSuggester ruian,
  [FromKeyedServices(AddressProvider.Mapy)] IAddressSuggester mapy,
  IAddressSuggestionCache cache)
  : IQueryHandler<SuggestAddressesQuery, IReadOnlyList<AddressSuggestion>>
{
  private static readonly TimeSpan SuccessTtl = TimeSpan.FromDays(180);
  private static readonly TimeSpan EmptyTtl = TimeSpan.FromDays(1);

  [GeneratedRegex(@"\s+")]
  private static partial Regex WhitespaceRegex();

  public async Task<Result<IReadOnlyList<AddressSuggestion>>> Handle(
    SuggestAddressesQuery query,
    CancellationToken cancellationToken)
  {
    string trimmedQuery = query.Query?.Trim() ?? string.Empty;

    if (trimmedQuery.Length <= 8)
    {
      return Result.Failure<IReadOnlyList<AddressSuggestion>>(
        new ValidationError([AddressErrors.QueryTooShort]));
    }

    if (query.Limit < 1)
    {
      return Result.Failure<IReadOnlyList<AddressSuggestion>>(
        new ValidationError([AddressErrors.LimitOutOfRange]));
    }

    string normalizedQuery = Normalize(trimmedQuery);
    string cacheKey = BuildCacheKey(query.Foreign, query.Limit, normalizedQuery);
    IReadOnlyList<AddressSuggestion>? cached = await cache.GetAsync(cacheKey, cancellationToken);
    if (cached is not null)
    {
      return Result.Success(cached);
    }

    IAddressSuggester primary = query.Foreign ? mapy : ruian;
    Result<IReadOnlyList<AddressSuggestion>> primaryResult =
      await primary.SuggestAsync(normalizedQuery, query.Limit, cancellationToken);

    if (primaryResult.IsSuccess && primaryResult.Value.Count > 0)
    {
      await cache.SetAsync(cacheKey, primaryResult.Value, SuccessTtl, cancellationToken);
      return primaryResult;
    }

    if (query.Foreign)
    {
      if (primaryResult.IsFailure)
      {
        return primaryResult;
      }

      await cache.SetAsync(cacheKey, primaryResult.Value, EmptyTtl, cancellationToken);
      return primaryResult;
    }

    Result<IReadOnlyList<AddressSuggestion>> fallbackResult =
      await mapy.SuggestAsync(normalizedQuery, query.Limit, cancellationToken);

    if (fallbackResult.IsFailure)
    {
      return Result.Failure<IReadOnlyList<AddressSuggestion>>(AddressErrors.ProviderUnavailable);
    }

    TimeSpan ttl = fallbackResult.Value.Count > 0 ? SuccessTtl : EmptyTtl;
    await cache.SetAsync(cacheKey, fallbackResult.Value, ttl, cancellationToken);
    return fallbackResult;
  }

  private static string Normalize(string trimmedQuery)
  {
#pragma warning disable CA1308 // Lowercase required for cache key consistency
    string lowered = trimmedQuery.ToLowerInvariant();
#pragma warning restore CA1308
    return WhitespaceRegex().Replace(lowered, " ");
  }

  private static string BuildCacheKey(bool foreign, int limit, string normalizedQuery) =>
    $"whisperer:v1:{(foreign ? "foreign" : "cz")}:{limit}:{normalizedQuery}";
}
