using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Abstractions.Gate;

namespace Infrastructure.ExternalServices.Gate;

internal sealed class HttpGateClient(HttpClient httpClient) : IGateClient
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
  };

  public async Task PutCardAsync(
    ulong uid, GateCardPayload payload, CancellationToken cancellationToken)
  {
    HttpResponseMessage response = await httpClient.PutAsJsonAsync(
      $"api/v1/cards/{uid.ToString(CultureInfo.InvariantCulture)}",
      payload,
      JsonOptions,
      cancellationToken);

    response.EnsureSuccessStatusCode();
  }

  public async Task DeleteCardAsync(ulong uid, CancellationToken cancellationToken)
  {
    HttpResponseMessage response = await httpClient.DeleteAsync(
      $"api/v1/cards/{uid.ToString(CultureInfo.InvariantCulture)}",
      cancellationToken);

    response.EnsureSuccessStatusCode();
  }
}
