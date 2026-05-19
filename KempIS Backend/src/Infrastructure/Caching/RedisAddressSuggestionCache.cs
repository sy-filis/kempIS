using System.Text.Json;
using Application.Abstractions.Addresses;
using Application.Addresses.Queries.SuggestAddresses;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Caching;

internal sealed class RedisAddressSuggestionCache(
  IDistributedCache cache,
  ILogger<RedisAddressSuggestionCache> logger)
  : IAddressSuggestionCache
{
  public async Task<IReadOnlyList<AddressSuggestion>?> GetAsync(
    string key,
    CancellationToken cancellationToken)
  {
    try
    {
      byte[]? bytes = await cache.GetAsync(key, cancellationToken);
      if (bytes is null)
      {
        return null;
      }

      return JsonSerializer.Deserialize<IReadOnlyList<AddressSuggestion>>(bytes);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogWarning(ex, "Redis cache read failed for key {Key}; treating as miss.", key);
      return null;
    }
  }

  public async Task SetAsync(
    string key,
    IReadOnlyList<AddressSuggestion> value,
    TimeSpan ttl,
    CancellationToken cancellationToken)
  {
    try
    {
      byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value);
      var entryOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
      await cache.SetAsync(key, bytes, entryOptions, cancellationToken);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      logger.LogWarning(ex, "Redis cache write failed for key {Key}; entry skipped.", key);
    }
  }
}
