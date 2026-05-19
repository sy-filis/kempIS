namespace Application.Abstractions.Gate;

public interface IGateClient
{
  Task PutCardAsync(ulong uid, GateCardPayload payload, CancellationToken cancellationToken);

  Task DeleteCardAsync(ulong uid, CancellationToken cancellationToken);
}

public sealed record GateCardPayload(DateTimeOffset ValidTo, string RealName, string Note);
