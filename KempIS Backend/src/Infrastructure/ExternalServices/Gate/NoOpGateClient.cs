using Application.Abstractions.Gate;
using Microsoft.Extensions.Logging;

namespace Infrastructure.ExternalServices.Gate;

internal sealed class NoOpGateClient : IGateClient
{
  public NoOpGateClient(ILogger<NoOpGateClient> logger) =>
    logger.LogInformation(
      "Gate webhook disabled: GateSystem:BaseUrl is not configured. Access-card issue/return calls will not be propagated.");

  public Task PutCardAsync(ulong uid, GateCardPayload payload, CancellationToken cancellationToken) =>
    Task.CompletedTask;

  public Task DeleteCardAsync(ulong uid, CancellationToken cancellationToken) =>
    Task.CompletedTask;
}
