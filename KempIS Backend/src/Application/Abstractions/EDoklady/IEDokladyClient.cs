using SharedKernel;

namespace Application.Abstractions.EDoklady;

public interface IEDokladyClient
{
  Task<Result<VirtualServiceCounter>> CreateVirtualServiceCounterAsync(
      CreateVirtualServiceCounterRequest request,
      CancellationToken cancellationToken);

  Task<Result<VirtualServiceCounter>> GetVirtualServiceCounterAsync(
      string id,
      CancellationToken cancellationToken);

  Task<Result<string>> StartPresentationAsync(
      string virtualServiceCounterId,
      CancellationToken cancellationToken);

  Task<Result<TransactionState>> GetTransactionAsync(
      string transactionId,
      CancellationToken cancellationToken);

  Task<Result<TransactionResult>> GetTransactionResultAsync(
      string transactionId,
      bool includeMDoc,
      bool includeMissingCredentials,
      CancellationToken cancellationToken);
}
